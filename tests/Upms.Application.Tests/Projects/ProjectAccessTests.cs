using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>The single authorization point with the Phase 1 rules (FR-008, FR-009, FR-015, research R7).</summary>
public sealed class ProjectAccessTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _owner = null!;
    private User _other = null!;
    private User _admin = null!;
    private Project _project = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _owner = await Data.UserAsync("owner");
        _other = await Data.UserAsync("other");
        _admin = await Data.AdministratorAsync();
        _project = await Data.ProjectAsync("WEB", _owner.Id);
    }

    private Task<Result<ProjectAccessInfo>> RequireAsync(ProjectRight right, string key = "WEB") =>
        CallAsync<IProjectAccess, Result<ProjectAccessInfo>>(a => a.RequireAsync(key, right, Ct));

    [Theory]
    [InlineData(ProjectRight.View)]
    [InlineData(ProjectRight.Contribute)]
    public async Task Any_active_user_can_view_and_contribute(ProjectRight right)
    {
        ActAs(_other);

        var result = await RequireAsync(right);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(_project.Id, result.Value!.ProjectId);
        Assert.Equal(_other.Id, result.Value.UserId);
        Assert.False(result.Value.CanManage);
    }

    [Fact]
    public async Task Only_the_owner_and_administrators_can_manage()
    {
        ActAs(_other);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Manage)).Error!.Kind);

        ActAs(_owner);
        Assert.True((await RequireAsync(ProjectRight.Manage)).Value!.CanManage);

        ActAs(_admin);
        var admin = (await RequireAsync(ProjectRight.Manage)).Value!;
        Assert.True(admin.CanManage);
        Assert.True(admin.IsAdministrator);
    }

    [Fact]
    public async Task Only_administrators_can_restore()
    {
        ActAs(_owner);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.Restore)).Error!.Kind);

        ActAs(_admin);
        Assert.True((await RequireAsync(ProjectRight.Restore)).IsSuccess);
    }

    [Fact]
    public async Task Creators_owners_and_administrators_can_delete_a_work_item()
    {
        var creatorId = _other.Id;

        ActAs(_other);
        Assert.True(await CallAsync<IProjectAccess, bool>(a => a.CanDeleteWorkItemAsync(_project.Id, creatorId, Ct)));

        ActAs(_owner);
        Assert.True(await CallAsync<IProjectAccess, bool>(a => a.CanDeleteWorkItemAsync(_project.Id, creatorId, Ct)));

        ActAs(_admin);
        Assert.True(await CallAsync<IProjectAccess, bool>(a => a.CanDeleteWorkItemAsync(_project.Id, creatorId, Ct)));

        var bystander = await Data.UserAsync("bystander");
        ActAs(bystander);
        Assert.False(await CallAsync<IProjectAccess, bool>(a => a.CanDeleteWorkItemAsync(_project.Id, creatorId, Ct)));
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
    public async Task Keys_are_matched_ignoring_case()
    {
        ActAs(_other);

        Assert.True((await RequireAsync(ProjectRight.View, "web")).IsSuccess);
    }

    [Fact]
    public async Task Anonymous_and_deactivated_callers_have_no_rights()
    {
        ActAs(null);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.View)).Error!.Kind);

        var gone = await Data.UserAsync("gone", isActive: false);
        ActAs(gone);
        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.View)).Error!.Kind);
    }

    [Fact]
    public async Task Rights_are_read_from_the_database_on_every_call()
    {
        ActAs(_owner);
        Assert.True((await RequireAsync(ProjectRight.Manage)).IsSuccess);

        await QueryAsync(async db =>
        {
            var owner = await db.Users.FindAsync([_owner.Id], Ct);
            owner!.Deactivate(Harness.Time.GetUtcNow());
            return await db.SaveChangesAsync(Ct);
        });

        Assert.Equal(ErrorKind.Forbidden, (await RequireAsync(ProjectRight.View)).Error!.Kind);
    }
}
