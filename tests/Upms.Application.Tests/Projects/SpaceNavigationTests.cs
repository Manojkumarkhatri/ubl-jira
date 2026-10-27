using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>Starred and recently opened spaces for the sidebar (Jira parity pass US4, FR-021, FR-021b; research R6, R7).</summary>
public sealed class SpaceNavigationTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _amina = null!;
    private readonly Dictionary<string, long> _ids = [];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _amina = await Data.UserAsync("amina");
        foreach (var (key, name) in new[] { ("WEB", "Website Revamp"), ("MOB", "Mobile App"), ("CBU", "Core Banking"), ("DOC", "Documentation") })
        {
            _ids[key] = (await Data.ProjectAsync(key, _amina.Id, name)).Id;
        }

        ActAs(_amina);
    }

    private async Task<SidebarView> SidebarAsync() =>
        (await CallAsync<ISpaceNavigationService, Result<SidebarView>>(s => s.GetSidebarAsync(Ct))).ValueOrThrow();

    private Task<Result> StarAsync(string key) => CallAsync<ISpaceNavigationService, Result>(s => s.StarAsync(key, Ct));

    private Task<Result> VisitAsync(string key) => CallAsync<ISpaceNavigationService, Result>(s => s.RecordVisitAsync(key, Ct));

    [Fact]
    public async Task JP_US4_AS6_Stars_are_personal_idempotent_and_come_first()
    {
        Assert.True((await StarAsync("MOB")).IsSuccess);
        Assert.True((await StarAsync("MOB")).IsSuccess);
        Assert.True((await StarAsync("CBU")).IsSuccess);
        await VisitAsync("WEB");

        var sidebar = await SidebarAsync();

        Assert.Equal(["Core Banking", "Mobile App"], sidebar.Starred.Select(s => s.Name));
        Assert.Equal(["CBU", "MOB", "WEB"], sidebar.Spaces.Select(s => s.Key));
        Assert.All(sidebar.Starred, s => Assert.True(s.IsStarred));
        Assert.Equal(2, await QueryAsync(db => db.StarredProjects.CountAsync(Ct)));

        var bilal = await Data.UserAsync("bilal");
        await Data.MemberAsync(_ids["MOB"], bilal.Id);
        ActAs(bilal);
        Assert.Empty((await SidebarAsync()).Starred);

        ActAs(_amina);
        Assert.True((await CallAsync<ISpaceNavigationService, Result>(s => s.UnstarAsync("MOB", Ct))).IsSuccess);
        Assert.Equal(["CBU"], (await SidebarAsync()).Starred.Select(s => s.Key));
    }

    [Fact]
    public async Task JP_US4_AS3_Recent_spaces_are_the_ones_opened_last_and_only_20_are_kept()
    {
        foreach (var key in new[] { "WEB", "MOB", "CBU", "WEB" })
        {
            Harness.Time.Advance(TimeSpan.FromMinutes(1));
            await VisitAsync(key);
        }

        Assert.Equal(["WEB", "CBU", "MOB"], (await SidebarAsync()).RecentSpaces.Select(s => s.Key));

        for (var i = 0; i < 22; i++)
        {
            var key = $"P{i:00}";
            _ids[key] = (await Data.ProjectAsync(key, _amina.Id, $"Space {i:00}")).Id;
            Harness.Time.Advance(TimeSpan.FromMinutes(1));
            await VisitAsync(key);
        }

        Assert.Equal(20, await QueryAsync(db => db.ProjectVisits.CountAsync(v => v.UserId == _amina.Id, Ct)));
        var sidebar = await SidebarAsync();
        Assert.Equal(5, sidebar.RecentSpaces.Count);
        Assert.Equal("P21", sidebar.RecentSpaces[0].Key);
        Assert.Equal(8, sidebar.Spaces.Count);
    }

    [Fact]
    public async Task JP_EdgeCase_A_space_one_can_no_longer_see_drops_out_but_its_rows_stay()
    {
        var bilal = await Data.UserAsync("bilal");
        await Data.MemberAsync(_ids["DOC"], bilal.Id);
        ActAs(bilal);
        await StarAsync("DOC");
        await VisitAsync("DOC");

        await Data.RemoveMemberAsync(_ids["DOC"], bilal.Id);

        var sidebar = await SidebarAsync();
        Assert.Empty(sidebar.Starred);
        Assert.Empty(sidebar.RecentSpaces);
        Assert.Empty(sidebar.Spaces);
        Assert.Equal(1, await QueryAsync(db => db.StarredProjects.CountAsync(Ct)));
    }

    [Fact]
    public async Task Non_members_cannot_star_or_visit_and_administrators_are_flagged()
    {
        var outsider = await Data.UserAsync("zara");
        ActAs(outsider);

        Assert.Equal(ErrorKind.NotFound, (await StarAsync("WEB")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await VisitAsync("WEB")).Error!.Kind);
        Assert.False((await SidebarAsync()).IsAdministrator);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await SidebarAsync()).IsAdministrator);
        Assert.True((await StarAsync("WEB")).IsSuccess);
    }

    [Fact]
    public async Task The_spaces_list_says_which_spaces_are_starred()
    {
        await StarAsync("MOB");

        var page = (await CallAsync<IProjectService, Result<Application.Common.Page<ProjectSummary>>>(s =>
            s.ListAsync(Application.Common.PageRequest.First, Ct))).ValueOrThrow();

        Assert.Equal(["MOB"], page.Items.Where(p => p.IsStarred).Select(p => p.Key));
    }
}
