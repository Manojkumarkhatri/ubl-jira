using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>The single authorization point with the Phase 2 membership rules (Phase 2 research R2; FR-002–FR-006,
/// FR-011).</summary>
public sealed class ProjectAccessTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _owner = null!;
    private User _member = null!;
    private User _viewer = null!;
    private User _outsider = null!;
    private User _admin = null!;
    private Project _project = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _owner = await Data.UserAsync("owner");
        _member = await Data.UserAsync("member");
        _viewer = await Data.UserAsync("viewer");
        _outsider = await Data.UserAsync("outsider");
        _admin = await Data.AdministratorAsync();
        _project = await Data.ProjectAsync("WEB", _owner.Id);
        await Data.MemberAsync(_project.Id, _member.Id, ProjectRole.Member);
        await Data.MemberAsync(_project.Id, _viewer.Id, ProjectRole.Viewer);
    }

    private Task<Result<ProjectAccessInfo>> RequireAsync(ProjectRight right, string key = "WEB") =>
        CallAsync<IProjectAccess, Result<ProjectAccessInfo>>(a => a.RequireAsync(key, right, Ct));

    private Task<Result<ProjectAccessInfo>> RequireByIdAsync(ProjectRight right) =>
        CallAsync<IProjectAccess, Result<ProjectAccessInfo>>(a => a.RequireAsync(_project.Id, right, Ct));

    private Task<bool> CanDeleteAsync(Guid creatorId) =>
        CallAsync<IProjectAccess, bool>(a => a.CanDeleteWorkItemAsync(_project.Id, creatorId, Ct));

    [Theory]
    [InlineData(ProjectRight.View)]
    [InlineData(ProjectRight.Contribute)]
    [InlineData(ProjectRight.DeleteOwnWorkItem)]
    [InlineData(ProjectRight.Manage)]
    [InlineData(ProjectRight.Restore)]
    public async Task P2_US1_AS4_Non_members_are_told_the_project_does_not_exist(ProjectRight right)
    {
        ActAs(_outsider);

        Assert.Equal(ErrorKind.NotFound, (await RequireAsync(right)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await RequireAsync(right, "web")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await RequireByIdAsync(right)).Error!.Kind);
    }

    [Fact]
    public async Task P2_US1_AS3_Viewers_can_look_but_not_change_anything()
    {
        ActAs(_viewer);

        var view = (await RequireAsync(ProjectRight.View)).Value!;
        Assert.Equal(ProjectRole.Viewer, view.Role);
        Assert.False(view.CanContribute);
        Assert.False(view.CanManage);
        foreach (var right in new[] { ProjectRight.Contribute, ProjectRight.DeleteOwnWorkItem, ProjectRight.Manage, ProjectRight.Restore })
        {
            Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(right)).Error!.Kind);
        }
    }

    [Fact]
    public async Task Members_view_and_contribute_but_do_not_manage()
    {
        ActAs(_member);

        var contribute = (await RequireAsync(ProjectRight.Contribute)).Value!;
        Assert.Equal(ProjectRole.Member, contribute.Role);
        Assert.True(contribute.CanContribute);
        Assert.False(contribute.CanManage);
        Assert.Equal(_member.Id, contribute.UserId);
        Assert.Equal(_project.Id, contribute.ProjectId);
        Assert.True((await RequireAsync(ProjectRight.DeleteOwnWorkItem)).IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Manage)).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Restore)).Error!.Kind);
    }

    [Fact]
    public async Task Project_Admins_also_manage_and_only_administrators_restore()
    {
        ActAs(_owner);

        var manage = (await RequireAsync(ProjectRight.Manage)).Value!;
        Assert.Equal(ProjectRole.ProjectAdmin, manage.Role);
        Assert.True(manage.CanManage);
        Assert.True(manage.CanContribute);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Restore)).Error!.Kind);
    }

    [Fact]
    public async Task P2_US1_AS8_Administrators_have_every_right_without_being_members()
    {
        ActAs(_admin);

        foreach (var right in Enum.GetValues<ProjectRight>())
        {
            var result = await RequireAsync(right);
            Assert.True(result.IsSuccess, $"{right}: {result.Error?.Message}");
            Assert.Null(result.Value!.Role);
            Assert.True(result.Value.IsAdministrator);
            Assert.True(result.Value.CanManage);
        }
    }

    [Fact]
    public async Task Creators_who_still_contribute_Project_Admins_and_administrators_can_delete_a_work_item()
    {
        ActAs(_member);
        Assert.True(await CanDeleteAsync(_member.Id));
        Assert.False(await CanDeleteAsync(_owner.Id));

        ActAs(_owner);
        Assert.True(await CanDeleteAsync(_member.Id));

        ActAs(_admin);
        Assert.True(await CanDeleteAsync(_member.Id));

        ActAs(_viewer);
        Assert.False(await CanDeleteAsync(_viewer.Id)); // a creator who is now a Viewer

        ActAs(_outsider);
        Assert.False(await CanDeleteAsync(_outsider.Id));
    }

    [Fact]
    public async Task Unknown_projects_are_not_found()
    {
        ActAs(_admin);

        Assert.Equal(ErrorKind.NotFound, (await RequireAsync(ProjectRight.View, "NOPE")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<IProjectAccess, Result<ProjectAccessInfo>>(a =>
            a.RequireAsync(987654, ProjectRight.View, Ct))).Error!.Kind);
    }

    [Fact]
    public async Task Anonymous_and_deactivated_callers_have_no_rights()
    {
        ActAs(null);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.View)).Error!.Kind);

        var gone = await Data.UserAsync("gone", isActive: false);
        await Data.MemberAsync(_project.Id, gone.Id);
        ActAs(gone);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.View)).Error!.Kind);
    }

    [Fact]
    public async Task P2_US1_AS5_A_removal_applies_at_the_next_call()
    {
        ActAs(_member);
        Assert.True((await RequireAsync(ProjectRight.Contribute)).IsSuccess);

        await QueryAsync(db => db.ProjectMembers.Where(m => m.UserId == _member.Id).ExecuteDeleteAsync(Ct));

        Assert.Equal(ErrorKind.NotFound, (await RequireAsync(ProjectRight.View)).Error!.Kind);
    }

    [Fact]
    public async Task P2_US1_AS6_A_role_change_applies_at_the_next_call()
    {
        ActAs(_viewer);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Contribute)).Error!.Kind);

        await QueryAsync(db => db.ProjectMembers.Where(m => m.UserId == _viewer.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Role, ProjectRole.Member), Ct));

        Assert.True((await RequireAsync(ProjectRight.Contribute)).IsSuccess);
    }
}
