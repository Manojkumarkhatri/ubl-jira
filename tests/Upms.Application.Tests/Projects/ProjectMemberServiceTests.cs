using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>Managing a project's team (Phase 2 FR-008–FR-013).</summary>
public sealed class ProjectMemberServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _owen = null!;
    private User _amina = null!;
    private User _bilal = null!;
    private Project _project = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _owen = await Data.UserAsync("owen");
        _amina = await Data.UserAsync("amina");
        _bilal = await Data.UserAsync("bilal");
        _project = await Data.ProjectAsync("WEB", _owen.Id, "Website Revamp");
        ActAs(_owen);
    }

    private Task<Result<TeamView>> TeamAsync() =>
        CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct));

    private async Task<int> VersionAsync() => (await TeamAsync()).Value!.MembersVersion;

    private async Task<Result<TeamView>> AddAsync(User user, ProjectRole role) =>
        await CallAsync<IProjectMemberService, Result<TeamView>>(async s => await s.AddAsync("WEB", user.Id, role, await VersionAsync(), Ct));

    private async Task<Result<TeamView>> ChangeRoleAsync(User user, ProjectRole role) =>
        await CallAsync<IProjectMemberService, Result<TeamView>>(async s =>
            await s.ChangeRoleAsync("WEB", user.Id, role, await VersionAsync(), Ct));

    private async Task<Result<TeamView>> RemoveAsync(User user) =>
        await CallAsync<IProjectMemberService, Result<TeamView>>(async s => await s.RemoveAsync("WEB", user.Id, await VersionAsync(), Ct));

    private Task<List<AuditEvent>> MembershipAuditAsync() =>
        QueryAsync(db => db.AuditEvents.AsNoTracking()
            .Where(e => e.EventType == AuditEventType.MemberAdded || e.EventType == AuditEventType.MemberRemoved
                || e.EventType == AuditEventType.MemberRoleChanged)
            .OrderBy(e => e.Id)
            .ToListAsync(Ct));

    [Fact]
    public async Task P2_US1_AS2_Members_are_added_with_roles_and_listed_Project_Admins_first()
    {
        await AddAsync(_bilal, ProjectRole.Viewer);
        var result = await AddAsync(_amina, ProjectRole.Member);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var team = result.Value!;
        Assert.Equal("WEB", team.ProjectKey);
        Assert.Equal("Website Revamp", team.ProjectName);
        Assert.True(team.CanManage);
        Assert.Equal(3, team.MembersVersion);
        Assert.Equal(["owen", "amina", "bilal"], team.Members.Select(m => m.UserName));
        Assert.Equal([ProjectRole.ProjectAdmin, ProjectRole.Member, ProjectRole.Viewer], team.Members.Select(m => m.Role));
        Assert.True(team.Members[0].IsMe);
        Assert.All(team.Members, m => Assert.True(m.IsActive));

        var audit = await MembershipAuditAsync();
        Assert.Equal(2, audit.Count);
        Assert.All(audit, e =>
        {
            Assert.Equal(AuditEventType.MemberAdded, e.EventType);
            Assert.Equal(_owen.Id, e.ActorUserId);
            Assert.Equal("WEB", e.Target);
        });
        Assert.Equal(_amina.Id, audit[1].SubjectUserId);
        Assert.Equal("""{"Role":"Member"}""", audit[1].Details);
    }

    [Fact]
    public async Task Every_member_can_see_the_team_but_only_Project_Admins_and_administrators_change_it()
    {
        await AddAsync(_amina, ProjectRole.Member);
        await AddAsync(_bilal, ProjectRole.Viewer);
        var version = await VersionAsync();

        ActAs(_bilal);
        var seen = (await TeamAsync()).Value!;
        Assert.False(seen.CanManage);
        Assert.True(seen.Members.Single(m => m.UserName == "bilal").IsMe);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IProjectMemberService, Result<TeamView>>(s =>
            s.ChangeRoleAsync("WEB", _bilal.Id, ProjectRole.Member, version, Ct))).Error!.Kind);

        ActAs(_amina);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IProjectMemberService, Result<TeamView>>(s =>
            s.RemoveAsync("WEB", _bilal.Id, version, Ct))).Error!.Kind);

        var carla = await Data.UserAsync("carla");
        ActAs(carla);
        Assert.Equal(ErrorKind.NotFound, (await TeamAsync()).Error!.Kind);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await ChangeRoleAsync(_bilal, ProjectRole.Member)).IsSuccess);
    }

    [Fact]
    public async Task People_are_found_by_name_user_name_or_email_leaving_out_members_and_deactivated_accounts()
    {
        await AddAsync(_amina, ProjectRole.Member);
        await Data.UserAsync("aminah");                 // matches "amin" by user name
        await Data.UserAsync("ali", isActive: false);  // deactivated
        await Data.UserAsync("zed");

        var byName = await CallAsync<IProjectMemberService, Result<IReadOnlyList<PersonOption>>>(s =>
            s.FindPeopleAsync("WEB", "amin", Ct));
        var byEmail = await CallAsync<IProjectMemberService, Result<IReadOnlyList<PersonOption>>>(s =>
            s.FindPeopleAsync("WEB", "zed@example", Ct));
        var deactivated = await CallAsync<IProjectMemberService, Result<IReadOnlyList<PersonOption>>>(s =>
            s.FindPeopleAsync("WEB", "ali", Ct));

        Assert.Equal(["aminah"], byName.Value!.Select(p => p.UserName));
        Assert.Equal(["zed"], byEmail.Value!.Select(p => p.UserName));
        Assert.Empty(deactivated.Value!);

        ActAs(_amina);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IProjectMemberService, Result<IReadOnlyList<PersonOption>>>(s =>
            s.FindPeopleAsync("WEB", "zed", Ct))).Error!.Kind);
    }

    [Fact]
    public async Task At_most_twenty_people_are_suggested()
    {
        for (var i = 0; i < 25; i++)
        {
            await Data.UserAsync($"person{i:00}");
        }

        var found = await CallAsync<IProjectMemberService, Result<IReadOnlyList<PersonOption>>>(s =>
            s.FindPeopleAsync("WEB", "person", Ct));

        Assert.Equal(20, found.Value!.Count);
    }

    [Fact]
    public async Task Deactivated_accounts_and_existing_members_cannot_be_added()
    {
        var gone = await Data.UserAsync("gone", isActive: false);
        await AddAsync(_bilal, ProjectRole.Viewer);

        Assert.Equal(ErrorCodes.AccountDeactivated, (await AddAsync(gone, ProjectRole.Member)).Error!.Code);
        var duplicate = (await AddAsync(_bilal, ProjectRole.Member)).Error!;
        Assert.Equal(ErrorCodes.DuplicateMember, duplicate.Code);
        Assert.Contains("Viewer", duplicate.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<IProjectMemberService, Result<TeamView>>(async s =>
            await s.AddAsync("WEB", Guid.NewGuid(), ProjectRole.Member, await VersionAsync(), Ct))).Error!.Kind);
    }

    [Fact]
    public async Task Role_changes_and_removals_apply_and_are_audited()
    {
        await AddAsync(_amina, ProjectRole.Member);

        var changed = await ChangeRoleAsync(_amina, ProjectRole.Viewer);
        var removed = await RemoveAsync(_amina);

        Assert.True(changed.IsSuccess, changed.Error?.Message);
        Assert.Equal(ProjectRole.Viewer, changed.Value!.Members.Single(m => m.UserName == "amina").Role);
        Assert.True(removed.IsSuccess, removed.Error?.Message);
        Assert.DoesNotContain(removed.Value!.Members, m => m.UserName == "amina");
        var audit = await MembershipAuditAsync();
        Assert.Equal(AuditEventType.MemberRoleChanged, audit[1].EventType);
        Assert.Equal("""{"From":"Member","To":"Viewer"}""", audit[1].Details);
        Assert.Equal(AuditEventType.MemberRemoved, audit[2].EventType);
        Assert.Equal("""{"Role":"Viewer"}""", audit[2].Details);
        Assert.Equal(ErrorKind.NotFound, (await RemoveAsync(_amina)).Error!.Kind);
    }

    [Fact]
    public async Task P2_US1_AS7_The_last_active_Project_Admin_cannot_leave_or_step_down()
    {
        await AddAsync(_amina, ProjectRole.Member);

        Assert.Equal(ErrorCodes.LastProjectAdmin, (await RemoveAsync(_owen)).Error!.Code);
        Assert.Equal(ErrorCodes.LastProjectAdmin, (await ChangeRoleAsync(_owen, ProjectRole.Member)).Error!.Code);

        await ChangeRoleAsync(_amina, ProjectRole.ProjectAdmin);
        Assert.True((await RemoveAsync(_owen)).IsSuccess);
    }

    [Fact]
    public async Task A_stale_team_version_is_a_conflict_that_returns_the_current_team()
    {
        var stale = await VersionAsync();
        await AddAsync(_amina, ProjectRole.Member);

        var result = await CallAsync<IProjectMemberService, Result<TeamView>>(s =>
            s.AddAsync("WEB", _bilal.Id, ProjectRole.Viewer, stale, Ct));

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        var current = Assert.IsType<TeamView>(result.Error.Current);
        Assert.Contains(current.Members, m => m.UserName == "amina");
        Assert.DoesNotContain(current.Members, m => m.UserName == "bilal");
    }

    [Fact]
    public async Task Two_Project_Admins_removing_each_other_at_the_same_moment_leave_one()
    {
        await AddAsync(_amina, ProjectRole.ProjectAdmin);
        var version = await VersionAsync();

        var results = await Task.WhenAll(
            InParallelAs(_owen, s => s.RemoveAsync("WEB", _amina.Id, version, Ct)),
            InParallelAs(_amina, s => s.RemoveAsync("WEB", _owen.Id, version, Ct)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.Error?.Kind is ErrorKind.Conflict or ErrorKind.NotFound);
        var admins = await QueryAsync(db => db.ProjectMembers.CountAsync(m => m.ProjectId == _project.Id && m.Role == ProjectRole.ProjectAdmin, Ct));
        Assert.Equal(1, admins);
    }

    private Task<Result<TeamView>> InParallelAs(User caller, Func<IProjectMemberService, Task<Result<TeamView>>> call) => Task.Run(async () =>
    {
        Harness.CurrentUser.UseInThisFlow(caller.Id);
        return await CallAsync(call);
    });
}
