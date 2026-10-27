using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>What the Projects module tells the Work and Notifications modules about who can see what (Phase 4 research
/// R6, R8, R15): the caller's projects, members to mention, and which people can see a project now.</summary>
public sealed class ProjectVisibilityTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _sara = null!;
    private Project _web = null!;
    private Project _mob = null!;
    private Project _pay = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _sara = await Data.UserAsync("sara");
        _web = await Data.ProjectAsync("WEB", _sara.Id);
        _mob = await Data.ProjectAsync("MOB", _sara.Id);
        _pay = await Data.ProjectAsync("PAY", _sara.Id);
    }

    private async Task<IReadOnlySet<long>?> VisibleAsync(User user)
    {
        ActAs(user);
        return await CallAsync<IProjectAccess, IReadOnlySet<long>?>(a => a.VisibleProjectIdsAsync(Ct));
    }

    private Task<IReadOnlyList<TeamMemberInfo>> FindAsync(long projectId, string term, int take = 10) =>
        CallAsync<IProjectTeam, IReadOnlyList<TeamMemberInfo>>(t => t.FindMembersAsync(projectId, term, take, Ct));

    private Task<IReadOnlySet<Guid>> WhoCanSeeAsync(long projectId, params User[] people) =>
        CallAsync<IProjectTeam, IReadOnlySet<Guid>>(t => t.WhoCanSeeAsync(projectId, people.Select(p => p.Id).ToList(), Ct));

    [Theory]
    [InlineData(ProjectRole.ProjectAdmin)]
    [InlineData(ProjectRole.Member)]
    [InlineData(ProjectRole.Viewer)]
    public async Task A_member_sees_their_projects_in_every_role(ProjectRole role)
    {
        var bilal = await Data.UserAsync("bilal");
        await Data.MemberAsync(_web.Id, bilal.Id, role);
        await Data.MemberAsync(_mob.Id, bilal.Id, role);

        Assert.Equal(new[] { _web.Id, _mob.Id }.Order(), (await VisibleAsync(bilal))!.Order());
    }

    [Fact]
    public async Task A_project_drops_out_once_the_person_is_removed()
    {
        var bilal = await Data.UserAsync("bilal");
        await Data.MemberAsync(_web.Id, bilal.Id);
        await Data.MemberAsync(_mob.Id, bilal.Id);

        await Data.RemoveMemberAsync(_web.Id, bilal.Id);

        Assert.Equal([_mob.Id], (await VisibleAsync(bilal))!.Order());
    }

    [Fact]
    public async Task Administrators_see_every_project_and_deactivated_people_none()
    {
        var admin = await Data.AdministratorAsync();
        var dora = await Data.UserAsync("dora", isActive: false);
        await Data.MemberAsync(_web.Id, dora.Id);

        Assert.Null(await VisibleAsync(admin));
        Assert.Empty((await VisibleAsync(dora))!);
        Assert.Empty((await VisibleAsync(await Data.UserAsync("nora")))!);
    }

    [Fact]
    public async Task Mention_suggestions_are_active_members_of_any_role_by_display_name_or_user_name()
    {
        var bilal = await Data.UserAsync("bilal");
        var bina = await Data.UserAsync("bina");
        var dora = await Data.UserAsync("dora", isActive: false);
        var bruno = await Data.UserAsync("bruno");
        var admin = await Data.AdministratorAsync("boss");
        await Data.MemberAsync(_web.Id, bilal.Id, ProjectRole.Viewer);
        await Data.MemberAsync(_web.Id, bina.Id);
        await Data.MemberAsync(_web.Id, dora.Id);
        await Data.MemberAsync(_mob.Id, bruno.Id);
        ActAs(_sara);

        var byUserName = await FindAsync(_web.Id, "bi");
        var byDisplayName = await FindAsync(_web.Id, "tester");

        // Bruno is not on WEB, Dora is deactivated, and the administrator is not a member.
        Assert.Equal([bilal.Id, bina.Id], byUserName.Select(m => m.UserId));
        Assert.Equal(["bilal", "bina"], byUserName.Select(m => m.UserName));
        Assert.Equal([bilal.Id, bina.Id, _sara.Id], byDisplayName.Select(m => m.UserId));
        Assert.DoesNotContain(admin.Id, byDisplayName.Select(m => m.UserId));
        Assert.Equal(2, (await FindAsync(_web.Id, "tester", take: 2)).Count);
    }

    [Fact]
    public async Task Only_members_and_administrators_can_see_a_project_now()
    {
        var bilal = await Data.UserAsync("bilal");
        var omar = await Data.UserAsync("omar");
        var dora = await Data.UserAsync("dora", isActive: false);
        var admin = await Data.AdministratorAsync();
        await Data.MemberAsync(_web.Id, bilal.Id, ProjectRole.Viewer);
        await Data.MemberAsync(_web.Id, dora.Id);
        await Data.MemberAsync(_pay.Id, omar.Id);
        ActAs(_sara);

        var seeing = await WhoCanSeeAsync(_web.Id, _sara, bilal, omar, dora, admin);

        Assert.Equal(new[] { _sara.Id, bilal.Id, admin.Id }.Order(), seeing.Order());
        Assert.Empty(await WhoCanSeeAsync(_web.Id));
    }
}
