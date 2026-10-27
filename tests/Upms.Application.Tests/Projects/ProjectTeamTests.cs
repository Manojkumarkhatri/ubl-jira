using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>What the Projects module offers the Work module about a team (Phase 2 research R7; Phase 3 research R16).</summary>
public sealed class ProjectTeamTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _owen = null!;
    private Project _project = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _owen = await Data.UserAsync("owen");
        _project = await Data.ProjectAsync("WEB", _owen.Id, "Website Revamp");
        ActAs(_owen);
    }

    private Task<Result<bool>> AddViewerAsync(Guid userId) =>
        CallAsync<IProjectTeam, Result<bool>>(t => t.AddViewerAsync(_project.Id, userId, Ct));

    [Fact]
    public async Task P3_US3_AS1_An_approver_from_outside_the_project_becomes_a_Viewer_and_it_is_audited()
    {
        var farah = await Data.UserAsync("farah");
        var before = await QueryAsync(db => db.Projects.Where(p => p.Id == _project.Id).Select(p => p.MembersVersion).SingleAsync(Ct));

        Assert.True((await AddViewerAsync(farah.Id)).ValueOrThrow());

        var member = await QueryAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == _project.Id && m.UserId == farah.Id, Ct));
        Assert.Equal(ProjectRole.Viewer, member.Role);
        Assert.Equal(before + 1, await QueryAsync(db => db.Projects.Where(p => p.Id == _project.Id).Select(p => p.MembersVersion).SingleAsync(Ct)));
        var added = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.MemberAdded);
        Assert.Equal((_owen.Id, (Guid?)farah.Id, "WEB"), (added.ActorUserId!.Value, added.SubjectUserId, added.Target));
    }

    [Fact]
    public async Task A_member_is_not_added_again_and_a_deactivated_account_is_refused()
    {
        var bilal = await Data.UserAsync("bilal");
        await Data.MemberAsync(_project.Id, bilal.Id);
        var dora = await Data.UserAsync("dora", isActive: false);

        Assert.False((await AddViewerAsync(bilal.Id)).ValueOrThrow());
        Assert.False((await AddViewerAsync(_owen.Id)).ValueOrThrow());
        Assert.Equal(ErrorCodes.AccountDeactivated, (await AddViewerAsync(dora.Id)).Error!.Code);
    }

    [Fact]
    public async Task Team_members_say_whether_their_account_is_active()
    {
        var viewer = await Data.UserAsync("vera");
        await Data.MemberAsync(_project.Id, viewer.Id, ProjectRole.Viewer);
        var dora = await Data.UserAsync("dora", isActive: false);
        await Data.MemberAsync(_project.Id, dora.Id);

        var team = await CallAsync<IProjectTeam, IReadOnlyList<TeamMemberInfo>>(t => t.GetMembersAsync(_project.Id, Ct));

        Assert.Equal((false, true), (team.Single(m => m.UserId == viewer.Id).CanWork, team.Single(m => m.UserId == viewer.Id).IsActive));
        Assert.Equal((false, false), (team.Single(m => m.UserId == dora.Id).CanWork, team.Single(m => m.UserId == dora.Id).IsActive));
    }
}
