using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>The search box and page (Phase 4 FR-032–FR-038, research R15, R16): key jumps, recent items, words across
/// every project the caller can see, filters and sorts; nothing from other projects, nothing deleted.</summary>
public sealed class SearchServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _paula = null!;

    /// <summary>amina owns WEB and MOB; paula owns PAY, which amina cannot see. Each has items about refunds.</summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Mobile App", "MOB", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
        _paula = await Data.UserAsync("paula");
        ActAs(_paula);
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Payments", "PAY", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
        await CreateAsync(WorkItemType.Task, "Refund ledger", projectKey: "PAY");
        ActAs(Owner);
        await CreateAsync(WorkItemType.Task, "Refund to card");                       // WEB-1
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await CreateAsync(WorkItemType.Bug, "Card declined", projectKey: "MOB");     // MOB-1
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await CreateAsync(WorkItemType.Task, "Weekly report");                        // WEB-2
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
    }

    private async Task<SearchResults> SearchAsync(SearchQuery query, int page = 1) =>
        (await CallAsync<ISearchService, Result<SearchResults>>(s => s.SearchAsync(query, new PageRequest(page), Ct))).ValueOrThrow();

    private async Task<IReadOnlyList<string>> KeysAsync(SearchQuery query) => (await SearchAsync(query)).Rows.Items.Select(r => r.Key).ToList();

    private Task<Result<string>> ResolveAsync(string text) => CallAsync<ISearchService, Result<string>>(s => s.ResolveKeyAsync(text, Ct));

    [Fact]
    public async Task P4_US3_AS1_A_key_opens_regardless_of_case()
    {
        Assert.Equal("MOB-1", (await ResolveAsync(" mob-1 ")).ValueOrThrow());
    }

    [Fact]
    public async Task P4_US3_AS2_An_unknown_key_and_a_hidden_key_get_the_same_answer()
    {
        var unknown = (await ResolveAsync("MOB-99")).Error!;
        var hidden = (await ResolveAsync("PAY-1")).Error!;
        var deleted = await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-2", Ct));
        var gone = (await ResolveAsync("WEB-2")).Error!;

        Assert.True(deleted.IsSuccess);
        Assert.Equal((ErrorKind.NotFound, "No work item MOB-99 was found."), (unknown.Kind, unknown.Message));
        Assert.Equal((ErrorKind.NotFound, "No work item PAY-1 was found."), (hidden.Kind, hidden.Message));
        Assert.Equal("No work item WEB-2 was found.", gone.Message);
    }

    [Fact]
    public async Task P4_US3_AS3_Recent_items_are_the_10_latest_still_visible_and_kept_to_50()
    {
        for (var i = 0; i < 52; i++)
        {
            await CreateAsync(WorkItemType.Task, $"Item {i}");
        }

        foreach (var key in Enumerable.Range(1, 54).Select(n => $"WEB-{n}"))
        {
            await DetailsAsync(key);
            Harness.Time.Advance(TimeSpan.FromSeconds(1));
        }

        await DetailsAsync("MOB-1");
        var recent = (await CallAsync<ISearchService, Result<IReadOnlyList<SearchResultRow>>>(s => s.RecentAsync(Ct))).ValueOrThrow();

        Assert.Equal(["MOB-1", "WEB-54", "WEB-53", "WEB-52", "WEB-51", "WEB-50", "WEB-49", "WEB-48", "WEB-47", "WEB-46"], recent.Select(r => r.Key));
        Assert.Equal(RecentView.Kept, await QueryAsync(db => db.RecentViews.CountAsync(Ct)));

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("MOB-1", Ct))).IsSuccess);
        Assert.DoesNotContain("MOB-1", (await CallAsync<ISearchService, Result<IReadOnlyList<SearchResultRow>>>(s => s.RecentAsync(Ct))).ValueOrThrow().Select(r => r.Key));
    }

    [Fact]
    public async Task P4_US3_AS4_Words_match_the_start_of_words_in_titles_descriptions_and_comments_across_projects()
    {
        await EditAsync("WEB-2", new WorkItemEdit.Description(Text("Includes refunded orders")));
        await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("MOB-1", Text("Customer asked for a refund"), Ct));

        var results = await SearchAsync(new SearchQuery("refund"));

        Assert.Equal(["WEB-2", "MOB-1", "WEB-1"], results.Rows.Items.Select(r => r.Key)); // newest update first; comments do not count
        Assert.Equal(3, results.Rows.TotalCount);
        var row = results.Rows.Items[1];
        Assert.Equal(("MOB", "Mobile App", WorkItemType.Bug, "Card declined", "To Do"), (row.ProjectKey, row.ProjectName, row.Type, row.Title, row.Status.Name));
        Assert.Equal(["MOB-1", "WEB-1"], await KeysAsync(new SearchQuery("REFUND card"))); // MOB-1: "card" in its title, "refund" in a comment
        Assert.Empty(await KeysAsync(new SearchQuery("refund nothing")));
    }

    [Fact]
    public async Task Results_come_50_at_a_time()
    {
        for (var i = 0; i < 55; i++)
        {
            await CreateAsync(WorkItemType.Task, $"Statement {i}");
        }

        var first = await SearchAsync(new SearchQuery("statement"));
        var second = await SearchAsync(new SearchQuery("statement"), page: 2);

        Assert.Equal((55, 50, 5), (first.Rows.TotalCount, first.Rows.Items.Count, second.Rows.Items.Count));
    }

    [Fact]
    public async Task P4_US3_AS5_Filters_narrow_the_results()
    {
        var bilal = await MemberAsync("bilal");
        await Data.MembersAsync("MOB", ProjectRole.Member, bilal);
        ActAs(Owner);
        await EditAsync("WEB-1", new WorkItemEdit.Assignee(Owner.Id));
        await EditAsync("WEB-1", new WorkItemEdit.Priority(Priority.High));
        await EditAsync("WEB-1", new WorkItemEdit.Dates(null, new DateOnly(2026, 10, 15)));
        await EditAsync("MOB-1", new WorkItemEdit.Assignee(bilal.Id));
        ActAs(bilal);
        await CreateAsync(WorkItemType.Task, "Bilal's task", projectKey: "MOB");
        ActAs(Owner);

        Assert.Equal(["MOB-2", "MOB-1"], await KeysAsync(new SearchQuery(null, ProjectKeys: ["mob"])));
        Assert.Equal(["MOB-1"], await KeysAsync(new SearchQuery(null, Types: [WorkItemType.Bug])));
        Assert.Equal(["WEB-1"], await KeysAsync(new SearchQuery(null, Priorities: [Priority.High])));
        Assert.Equal(["WEB-1"], await KeysAsync(new SearchQuery(null, AssignedToMe: true)));
        Assert.Equal(["MOB-1"], await KeysAsync(new SearchQuery(null, Assignees: [bilal.Id])));
        Assert.Equal(["MOB-2", "WEB-2"], await KeysAsync(new SearchQuery(null, Unassigned: true)));
        Assert.Equal(["MOB-2"], await KeysAsync(new SearchQuery(null, Creators: [bilal.Id])));
        Assert.Equal(["WEB-1"], await KeysAsync(new SearchQuery(null, Due: new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)))));
        Assert.Equal(4, (await KeysAsync(new SearchQuery(null, Categories: [StatusCategory.ToDo]))).Count);
        Assert.Empty(await KeysAsync(new SearchQuery(null, Categories: [StatusCategory.Done])));
        var today = DateOnly.FromDateTime(Harness.Time.GetUtcNow().UtcDateTime);
        Assert.Equal(4, (await KeysAsync(new SearchQuery(null, Created: new DateRange(today, today)))).Count);
        Assert.Empty(await KeysAsync(new SearchQuery(null, Updated: new DateRange(today.AddDays(1), null))));
    }

    [Theory]
    [InlineData(SearchSort.Updated, true, "WEB-2,MOB-1,WEB-1")]
    [InlineData(SearchSort.Created, false, "WEB-1,MOB-1,WEB-2")]
    [InlineData(SearchSort.Key, false, "MOB-1,WEB-1,WEB-2")]
    [InlineData(SearchSort.Priority, false, "WEB-1,MOB-1,WEB-2")]
    [InlineData(SearchSort.DueDate, false, "WEB-2,MOB-1,WEB-1")]
    public async Task P4_US3_AS6_Results_sort_by_each_choice(SearchSort sort, bool descending, string expected)
    {
        await EditAsync("WEB-1", new WorkItemEdit.Priority(Priority.Highest));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync("WEB-2", new WorkItemEdit.Dates(null, new DateOnly(2026, 10, 1)));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync("MOB-1", new WorkItemEdit.Dates(null, new DateOnly(2026, 10, 9)));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync("WEB-2", new WorkItemEdit.Priority(Priority.Low)); // WEB-2 is updated last

        Assert.Equal(expected.Split(','), await KeysAsync(new SearchQuery(null, Sort: sort, Descending: descending)));
    }

    [Fact]
    public async Task P4_US3_AS7_Hidden_projects_and_deleted_items_never_appear_and_administrators_see_everything()
    {
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).IsSuccess);

        var mine = await SearchAsync(new SearchQuery("refund"));
        Assert.Empty(mine.Rows.Items);
        Assert.Equal(0, mine.Rows.TotalCount);

        var hiddenProject = await SearchAsync(new SearchQuery("refund", ProjectKeys: ["PAY"]));
        Assert.Empty(hiddenProject.Rows.Items);
        Assert.Equal(["Space PAY"], hiddenProject.UnavailableValues);

        ActAs(await Data.AdministratorAsync());
        Assert.Equal(["PAY-1"], await KeysAsync(new SearchQuery("refund")));
    }

    [Fact]
    public async Task Signed_out_callers_search_nothing()
    {
        ActAs(null);

        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<ISearchService, Result<SearchResults>>(s =>
            s.SearchAsync(new SearchQuery("refund"), PageRequest.First, Ct))).Error!.Kind);
    }

    [Fact]
    public async Task P4_US4_AS4_Results_carry_labels_and_P4_US4_AS6_search_filters_by_any_of_several()
    {
        await LabelAsync("WEB-1", "launch");
        await LabelAsync("MOB-1", "Payments");
        await LabelAsync("MOB-1", "launch");
        ActAs(_paula);
        await LabelAsync("PAY-1", "launch");
        ActAs(Owner);

        var results = await SearchAsync(new SearchQuery(null, Labels: ["LAUNCH"]));

        Assert.Equal(["MOB-1", "WEB-1"], results.Rows.Items.Select(r => r.Key));
        Assert.Equal(["launch", "Payments"], results.Rows.Items[0].Labels);
        Assert.Equal(["MOB-1", "WEB-1"], await KeysAsync(new SearchQuery(null, Labels: ["payments", "launch"])));
        Assert.Equal(["WEB-1"], await KeysAsync(new SearchQuery("refund", Labels: ["launch"])));
    }

    private async Task LabelAsync(string key, string label) =>
        (await CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync(key, label, Ct))).ValueOrThrow();
}
