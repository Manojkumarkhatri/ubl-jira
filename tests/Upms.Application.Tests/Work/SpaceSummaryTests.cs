using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The space Summary page, with Jira's summary sections (Jira parity pass US4 scenario 7, FR-022; research R8). WEB
/// is amina's Kanban space; the clock starts on 27 September 2026.</summary>
public sealed class SpaceSummaryTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static readonly DateOnly Today = new(2026, 9, 27);

    private User _omar = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _omar = await MemberAsync("omar");
        ActAs(Owner);
    }

    private async Task<SpaceSummary> SummaryAsync(string key = "WEB") =>
        (await CallAsync<ISpaceSummaryService, Result<SpaceSummary>>(s => s.GetAsync(key, Today, Ct))).ValueOrThrow();

    [Fact]
    public async Task JP_US4_AS7_The_last_7_days_and_what_is_due_soon()
    {
        var old = await AddAsync(ToDo, "Old task");                        // created 20 days ago, never touched since
        Harness.Time.Advance(TimeSpan.FromDays(20));
        var done = await AddAsync(ToDo, "Done this week");
        (await EditAsync(done.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();
        var dueSoon = await AddAsync(ToDo, "Due soon");
        (await EditAsync(dueSoon.Key, new WorkItemEdit.Dates(null, Today.AddDays(6)))).ValueOrThrow();
        var overdue = await AddAsync(ToDo, "Overdue");
        (await EditAsync(overdue.Key, new WorkItemEdit.Dates(null, Today.AddDays(-1)))).ValueOrThrow();
        var later = await AddAsync(ToDo, "Later");
        (await EditAsync(later.Key, new WorkItemEdit.Dates(null, Today.AddDays(8)))).ValueOrThrow();
        var closedDue = await AddAsync(ToDo, "Done but due");
        (await EditAsync(closedDue.Key, new WorkItemEdit.Dates(null, Today.AddDays(1)))).ValueOrThrow();
        (await EditAsync(closedDue.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();

        var summary = await SummaryAsync();

        Assert.Equal(new SummaryCounts(Completed: 2, Updated: 5, Created: 5, DueSoon: 2), summary.Last7Days);
        Assert.NotNull(old);
    }

    [Fact]
    public async Task JP_US4_AS7_Status_overview_priorities_types_and_workload()
    {
        var a = await AddAsync(ToDo, "One", WorkItemType.Bug);
        var b = await AddAsync(ToDo, "Two");
        var c = await AddAsync(ToDo, "Three", WorkItemType.Story);
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        (await EditAsync(a.Key, new WorkItemEdit.Status(InProgress))).ValueOrThrow();
        (await EditAsync(b.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();
        (await EditAsync(a.Key, new WorkItemEdit.Priority(Priority.High))).ValueOrThrow();
        (await EditAsync(a.Key, new WorkItemEdit.Assignee(_omar.Id))).ValueOrThrow();
        (await EditAsync(c.Key, new WorkItemEdit.Assignee(_omar.Id))).ValueOrThrow();
        (await EditAsync(b.Key, new WorkItemEdit.Assignee(Owner.Id))).ValueOrThrow();
        var deleted = await AddAsync(ToDo, "Gone");
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(deleted.Key, Ct))).IsSuccess);

        var summary = await SummaryAsync();

        Assert.Equal([(StatusCategory.ToDo, 2), (StatusCategory.InProgress, 1), (StatusCategory.Done, 1)],
            summary.StatusOverview.Select(s => (s.Category, s.Count)));
        Assert.Equal(1, summary.Priorities.Single(p => p.Priority == Priority.High).Count);
        Assert.Equal(3, summary.Priorities.Single(p => p.Priority == Priority.Medium).Count);
        Assert.Equal([(WorkItemType.Epic, 1), (WorkItemType.Story, 1), (WorkItemType.Task, 1), (WorkItemType.Bug, 1)],
            summary.Types.Select(t => (t.Type, t.Count)));
        // Open items per person; done work does not count, and unassigned open work is its own row.
        Assert.Equal([("Omar Tester", 2), ((string?)null, 1)], summary.Workload.Select(w => (w.Assignee?.DisplayName, w.OpenCount)));
    }

    [Fact]
    public async Task JP_US4_AS7_Epic_progress_and_the_latest_changes()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");                          // WEB-1
        var story = await CreateAsync(WorkItemType.Story, "Refund to card", "WEB-1");  // WEB-2
        await CreateAsync(WorkItemType.Bug, "Refund shows twice", "WEB-1");             // WEB-3
        (await EditAsync(story.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();
        for (var i = 0; i < 22; i++)
        {
            Harness.Time.Advance(TimeSpan.FromMinutes(1));
            (await EditAsync("WEB-3", new WorkItemEdit.Title($"Refund shows twice ({i})"))).ValueOrThrow();
        }

        var summary = await SummaryAsync();

        var epic = Assert.Single(summary.Containers);
        Assert.Equal(("WEB-1", "Card refunds", new Progress(1, 2)), (epic.Key, epic.Title, epic.Progress));
        Assert.Equal(20, summary.RecentActivity.Count);
        var latest = summary.RecentActivity[0];
        Assert.Equal(("WEB-3", WorkItemField.Title, "Refund shows twice (21)", "Amina Tester"), (latest.ItemKey, latest.Field, latest.NewValue, latest.ActorName));
        Assert.True(summary.RecentActivity.Zip(summary.RecentActivity.Skip(1)).All(p => p.First.At >= p.Second.At));
    }

    [Fact]
    public async Task Activity_older_than_30_days_and_of_other_spaces_is_left_out()
    {
        await AddAsync(ToDo, "Long ago");
        Harness.Time.Advance(TimeSpan.FromDays(31));
        (await CallAsync<Upms.Application.Projects.IProjectService, Result<string>>(s => s.CreateAsync("Payments", "PAY", null, ProjectTemplate.Kanban, Ct))).ValueOrThrow();
        await CreateAsync(WorkItemType.Task, "Elsewhere", projectKey: "PAY");

        Assert.Empty((await SummaryAsync()).RecentActivity);
    }

    [Fact]
    public async Task An_empty_space_gives_zeros_and_non_members_get_not_found()
    {
        var summary = await SummaryAsync();

        Assert.Equal(new SummaryCounts(0, 0, 0, 0), summary.Last7Days);
        Assert.All(summary.StatusOverview, s => Assert.Equal(0, s.Count));
        Assert.Empty(summary.Workload);
        Assert.Empty(summary.Containers);

        ActAs(await Data.UserAsync("zara"));
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<ISpaceSummaryService, Result<SpaceSummary>>(s => s.GetAsync("WEB", Today, Ct))).Error!.Kind);
    }
}

/// <summary>"For you", Worked on: items the person created, changed or commented on lately (Jira parity pass FR-022a; research R9).</summary>
public sealed class WorkedOnTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private async Task<Page<SearchResultRow>> WorkedOnAsync(int page = 1) =>
        (await CallAsync<ISearchService, Result<Page<SearchResultRow>>>(s => s.WorkedOnAsync(new PageRequest(page), Ct))).ValueOrThrow();

    [Fact]
    public async Task JP_US4_AS8_Items_created_changed_or_commented_on_in_30_days_newest_first_once_each()
    {
        var omar = await MemberAsync("omar");
        ActAs(Owner);
        var mine = await AddAsync(ToDo, "Mine");
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        ActAs(omar);
        var his = await AddAsync(ToDo, "His");
        var commented = await AddAsync(ToDo, "Commented by me");
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        ActAs(Owner);
        (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync(commented.Key, Documents.Text("Looks good."), Ct))).ValueOrThrow();
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        (await EditAsync(mine.Key, new WorkItemEdit.Title("Mine, renamed"))).ValueOrThrow();

        var rows = await WorkedOnAsync();

        Assert.Equal([mine.Key, commented.Key], rows.Items.Select(r => r.Key));
        Assert.DoesNotContain(his.Key, rows.Items.Select(r => r.Key));
        Assert.Equal(("WEB", "Website Revamp"), (rows.Items[0].ProjectKey, rows.Items[0].ProjectName));

        Harness.Time.Advance(TimeSpan.FromDays(31));
        Assert.Empty((await WorkedOnAsync()).Items);
    }

    [Fact]
    public async Task Deleted_items_and_spaces_one_cannot_see_are_left_out()
    {
        var bilal = await MemberAsync("bilal");
        ActAs(bilal);
        var gone = await AddAsync(ToDo, "Gone");
        var kept = await AddAsync(ToDo, "Kept");
        ActAs(Owner);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(gone.Key, Ct))).IsSuccess);
        ActAs(bilal);
        Assert.Equal([kept.Key], (await WorkedOnAsync()).Items.Select(r => r.Key));

        var webId = await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.Id).SingleAsync(Ct));
        await Data.RemoveMemberAsync(webId, bilal.Id);
        Assert.Empty((await WorkedOnAsync()).Items);
    }
}
