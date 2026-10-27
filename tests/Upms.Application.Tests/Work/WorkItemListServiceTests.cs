using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The List view (Phase 2 FR-028–FR-030, research R11). WEB is amina's project; "today" is 27 Sep 2026.</summary>
public sealed class WorkItemListServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static readonly DateOnly Today = new(2026, 9, 27);

    private User _bilal = null!;
    private User _cara = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        _cara = await MemberAsync("cara");
    }

    private async Task<WorkItemListView> ListAsync(WorkItemListQuery? query = null) =>
        (await CallAsync<IWorkItemListService, Result<WorkItemListView>>(s => s.ListAsync("WEB", query ?? new WorkItemListQuery(), Today, Ct)))
        .ValueOrThrow();

    private async Task<IReadOnlyList<string>> KeysAsync(WorkItemListQuery query) =>
        (await ListAsync(query)).Rows.Items.Select(r => r.Key).ToList();

    private static List<string> Keys(params int[] numbers) => numbers.Select(n => $"WEB-{n}").ToList();

    /// <summary>Creates a task and sets its fields, one minute after the previous change.</summary>
    private async Task TaskAsync(string title, Priority priority = Priority.Medium, long? column = null, User? assignee = null,
        DateOnly? start = null, DateOnly? due = null, string? description = null)
    {
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        var card = await AddAsync(ToDo, title);
        if (priority != Priority.Medium)
        {
            await EditAsync(card.Key, new WorkItemEdit.Priority(priority));
        }

        if (assignee is not null)
        {
            await EditAsync(card.Key, new WorkItemEdit.Assignee(assignee.Id));
        }

        if (start is not null || due is not null)
        {
            await EditAsync(card.Key, new WorkItemEdit.Dates(start, due));
        }

        if (description is not null)
        {
            await EditAsync(card.Key, new WorkItemEdit.Description(Documents.Text(description)));
        }

        if (column is { } columnId && columnId != ToDo)
        {
            await EditAsync(card.Key, new WorkItemEdit.Status(columnId));
        }
    }

    /// <summary>Five tasks that sort differently by every column.</summary>
    private async Task SortingSetUpAsync()
    {
        await TaskAsync("Charlie", Priority.Low, ToDo, _bilal, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5));
        await TaskAsync("alpha", Priority.Highest, InProgress);
        await TaskAsync("Bravo", Priority.Medium, Done, Owner, due: new DateOnly(2026, 9, 20));
        await TaskAsync("delta", Priority.High, ToDo, _cara, new DateOnly(2026, 9, 25), new DateOnly(2026, 10, 1));
        await TaskAsync("Echo", Priority.Lowest, InProgress, _bilal, new DateOnly(2026, 9, 28));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync("WEB-1", new WorkItemEdit.Description(Documents.Text("Touched last")));
    }

    /// <summary>Six tasks around "today" for the filters.</summary>
    private async Task FilteringSetUpAsync()
    {
        await TaskAsync("Fix the login bug", Priority.High, ToDo, Owner, due: new DateOnly(2026, 9, 26), description: "Users see an error on sign-in");
        await TaskAsync("Write release notes", Priority.Medium, InProgress, due: Today);
        await TaskAsync("Plan the login redesign", Priority.Low, ToDo, _bilal, due: Today.AddDays(7));
        await TaskAsync("Order swag", Priority.Medium, ToDo, _bilal, due: Today.AddDays(8));
        await TaskAsync("Close the login incident", Priority.Medium, Done, Owner, due: new DateOnly(2026, 9, 20));
        await TaskAsync("Brainstorm");
    }

    [Fact]
    public async Task P2_US3_AS1_Tasks_and_sub_tasks_are_listed_newest_first_50_to_a_page_with_the_total()
    {
        for (var i = 1; i <= 54; i++)
        {
            await AddAsync(ToDo, $"Task {i}");
        }

        await AddSubtaskAsync("WEB-1", "A sub-task");

        var first = await ListAsync();
        var second = await ListAsync(new WorkItemListQuery(Page: 2));

        Assert.Equal((55, 50, 50), (first.Rows.TotalCount, first.Rows.PageSize, first.Rows.Items.Count));
        Assert.Equal(["WEB-55", "WEB-54", "WEB-53"], first.Rows.Items.Take(3).Select(r => r.Key));
        Assert.Equal(("A sub-task", "WEB-1"), (first.Rows.Items[0].Title, first.Rows.Items[0].ParentKey));
        Assert.Null(first.Rows.Items[1].ParentKey);
        Assert.Equal(Keys(5, 4, 3, 2, 1), second.Rows.Items.Select(r => r.Key));
    }

    [Fact]
    public async Task P2_US3_AS1_Rows_carry_what_the_table_shows()
    {
        await SortingSetUpAsync();

        var rows = (await ListAsync()).Rows.Items;

        var charlie = rows.Single(r => r.Key == "WEB-1");
        Assert.Equal(("Charlie", "To Do", Priority.Low, true), (charlie.Title, charlie.Status.Name, charlie.Priority, charlie.IsOpen));
        Assert.Equal(new AssigneeRef(_bilal.Id, "Bilal Tester", "BT", CanWork: true), charlie.Assignee);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5)), (charlie.StartDate!.Value, charlie.DueDate!.Value));
        Assert.Equal(Harness.Time.GetUtcNow(), charlie.UpdatedAt);
        Assert.False(rows.Single(r => r.Key == "WEB-3").IsOpen);
        Assert.Null(rows.Single(r => r.Key == "WEB-2").Assignee);
    }

    [Theory]
    [InlineData(ListSort.Key, false, new[] { 1, 2, 3, 4, 5 })]
    [InlineData(ListSort.Key, true, new[] { 5, 4, 3, 2, 1 })]
    [InlineData(ListSort.Title, false, new[] { 2, 3, 1, 4, 5 })]
    [InlineData(ListSort.Title, true, new[] { 5, 4, 1, 3, 2 })]
    [InlineData(ListSort.Status, false, new[] { 1, 4, 2, 5, 3 })]
    [InlineData(ListSort.Status, true, new[] { 3, 5, 2, 4, 1 })]
    [InlineData(ListSort.Priority, false, new[] { 2, 4, 3, 1, 5 })]
    [InlineData(ListSort.Priority, true, new[] { 5, 1, 3, 4, 2 })]
    [InlineData(ListSort.Assignee, false, new[] { 3, 1, 5, 4, 2 })]
    [InlineData(ListSort.Assignee, true, new[] { 4, 5, 1, 3, 2 })]
    [InlineData(ListSort.StartDate, false, new[] { 4, 5, 1, 2, 3 })]
    [InlineData(ListSort.StartDate, true, new[] { 1, 5, 4, 3, 2 })]
    [InlineData(ListSort.DueDate, false, new[] { 3, 4, 1, 2, 5 })]
    [InlineData(ListSort.DueDate, true, new[] { 1, 4, 3, 5, 2 })]
    [InlineData(ListSort.Updated, false, new[] { 2, 3, 4, 5, 1 })]
    [InlineData(ListSort.Updated, true, new[] { 1, 5, 4, 3, 2 })]
    public async Task P2_US3_AS2_Every_column_sorts_both_ways(ListSort sort, bool descending, int[] expected)
    {
        await SortingSetUpAsync();

        var keys = await KeysAsync(new WorkItemListQuery(sort, descending));

        Assert.Equal(Keys(expected), keys);
    }

    [Fact]
    public async Task P2_US3_AS3_The_due_filters_follow_the_given_today_and_leave_out_done_tasks()
    {
        await FilteringSetUpAsync();

        Assert.Equal(Keys(1), await KeysAsync(new WorkItemListQuery(Due: DueFilter.Overdue)));
        Assert.Equal(Keys(3, 2), await KeysAsync(new WorkItemListQuery(Due: DueFilter.Next7Days)));
        Assert.Equal(Keys(6), await KeysAsync(new WorkItemListQuery(Due: DueFilter.NoDueDate)));
    }

    [Fact]
    public async Task P2_US3_AS3_Assignee_filters_combine_as_either_or()
    {
        await FilteringSetUpAsync();

        Assert.Equal(Keys(5, 1), await KeysAsync(new WorkItemListQuery(AssignedToMe: true)));
        Assert.Equal(Keys(6, 2), await KeysAsync(new WorkItemListQuery(Unassigned: true)));
        Assert.Equal(Keys(4, 3), await KeysAsync(new WorkItemListQuery(AssigneeIds: [_bilal.Id])));
        Assert.Equal(Keys(6, 5, 2, 1), await KeysAsync(new WorkItemListQuery(AssignedToMe: true, Unassigned: true)));
    }

    [Fact]
    public async Task P2_US3_AS3_Status_status_type_and_priority_filters()
    {
        await FilteringSetUpAsync();

        Assert.Equal(Keys(6, 4, 3, 1), await KeysAsync(new WorkItemListQuery(ColumnIds: [ToDo])));
        Assert.Equal(Keys(2), await KeysAsync(new WorkItemListQuery(Categories: [StatusCategory.InProgress])));
        Assert.Equal(Keys(5, 2), await KeysAsync(new WorkItemListQuery(Categories: [StatusCategory.InProgress, StatusCategory.Done])));
        Assert.Equal(Keys(3, 1), await KeysAsync(new WorkItemListQuery(Priorities: [Priority.High, Priority.Low])));
    }

    [Fact]
    public async Task P2_US3_AS3_Words_are_found_in_titles_and_descriptions_regardless_of_case()
    {
        await FilteringSetUpAsync();

        Assert.Equal(Keys(5, 3, 1), await KeysAsync(new WorkItemListQuery(Text: "login")));
        Assert.Equal(Keys(1), await KeysAsync(new WorkItemListQuery(Text: "LOGIN sign-in")));
        Assert.Equal(Keys(1), await KeysAsync(new WorkItemListQuery(Text: "error")));
        Assert.Empty(await KeysAsync(new WorkItemListQuery(Text: "%"))); // typed characters are not wildcards
        Assert.Empty(await KeysAsync(new WorkItemListQuery(Text: "_")));
    }

    [Fact]
    public async Task P2_US3_AS3_Filters_combine_and_the_count_follows()
    {
        await FilteringSetUpAsync();

        var list = await ListAsync(new WorkItemListQuery(Categories: [StatusCategory.ToDo], Text: "login"));
        var narrow = await KeysAsync(new WorkItemListQuery(ColumnIds: [ToDo], AssigneeIds: [_bilal.Id], Due: DueFilter.Next7Days));

        Assert.Equal(Keys(3, 1), list.Rows.Items.Select(r => r.Key));
        Assert.Equal(2, list.Rows.TotalCount);
        Assert.Equal(Keys(3), narrow);
    }

    [Fact]
    public async Task Thousands_of_filter_values_from_an_address_are_answered_as_if_only_the_meaningful_ones_were_given()
    {
        // A shared address can be edited by anyone who has it, so its lists may be long and hold values that mean nothing
        // here (Phase 2 security review, V5).
        await FilteringSetUpAsync();
        var unknownColumns = Enumerable.Range(1, 5000).Select(n => ToDo + 100_000 + n).ToList();
        var unknownPeople = Enumerable.Range(0, 5000).Select(_ => Guid.NewGuid()).ToList();

        Assert.Equal(Keys(6, 4, 3, 1), await KeysAsync(new WorkItemListQuery(ColumnIds: [.. unknownColumns, ToDo])));
        Assert.Equal(Keys(4, 3), await KeysAsync(new WorkItemListQuery(AssigneeIds: [.. unknownPeople, _bilal.Id])));
        Assert.Empty(await KeysAsync(new WorkItemListQuery(ColumnIds: unknownColumns)));
        Assert.Empty(await KeysAsync(new WorkItemListQuery(AssigneeIds: unknownPeople)));
        Assert.Equal(Keys(6, 2), await KeysAsync(new WorkItemListQuery(Unassigned: true, AssigneeIds: unknownPeople)));
    }

    [Fact]
    public async Task The_view_offers_the_columns_the_people_and_the_first_to_do_column()
    {
        var dan = await MemberAsync("dan");
        await TaskAsync("Handed over", assignee: dan);
        var team = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct))).ValueOrThrow();
        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.RemoveAsync("WEB", dan.Id, team.MembersVersion, Ct))).ValueOrThrow();

        var view = await ListAsync();

        Assert.Equal(("WEB", "Website Revamp", true, ToDo), (view.ProjectKey, view.ProjectName, view.CanContribute, view.FirstToDoColumnId));
        Assert.Equal(["To Do", "In Progress", "Done"], view.Columns.Select(c => c.Name));
        Assert.Equal([("Amina Tester", true), ("Bilal Tester", false), ("Cara Tester", false), ("Dan Tester", false)],
            view.People.Select(p => (p.DisplayName, p.IsMe)));
        Assert.True(view.CanManage);
    }

    [Fact]
    public async Task Viewers_can_read_the_list_but_not_add_to_it()
    {
        var vera = await MemberAsync("vera", ProjectRole.Viewer);
        await TaskAsync("Visible to all members");
        ActAs(vera);

        var view = await ListAsync();

        Assert.False(view.CanContribute);
        Assert.False(view.CanManage);
        Assert.Equal(Keys(1), view.Rows.Items.Select(r => r.Key));
    }

    [Fact]
    public async Task Non_members_are_told_the_project_does_not_exist()
    {
        ActAs(await Data.UserAsync("nora"));

        var result = await CallAsync<IWorkItemListService, Result<WorkItemListView>>(s => s.ListAsync("WEB", new WorkItemListQuery(), Today, Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_keeps_the_total()
    {
        await TaskAsync("Only one");

        var view = await ListAsync(new WorkItemListQuery(Page: 3));

        Assert.Empty(view.Rows.Items);
        Assert.Equal((1, 3), (view.Rows.TotalCount, view.Rows.PageNumber));
    }

    /// <summary>An epic with a story and a bug, a story in no epic, and a sub-task (Phase 3 US1).</summary>
    private async Task TypesSetUpAsync()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");                          // WEB-1
        await CreateAsync(WorkItemType.Story, "Refund to card", containerKey: "WEB-1"); // WEB-2
        await CreateAsync(WorkItemType.Bug, "Refund shows twice", containerKey: "WEB-1"); // WEB-3
        await CreateAsync(WorkItemType.Story, "Statements page");                      // WEB-4
        await AddSubtaskAsync("WEB-4", "Draw the table");                              // WEB-5
        await EditAsync("WEB-2", new WorkItemEdit.Estimate(3));
    }

    [Fact]
    public async Task P3_US1_AS8_Every_type_is_listed_with_its_type_epic_and_estimate()
    {
        await TypesSetUpAsync();

        var rows = (await ListAsync()).Rows.Items;

        Assert.Equal(Keys(5, 4, 3, 2, 1), rows.Select(r => r.Key));
        Assert.Equal([WorkItemType.Subtask, WorkItemType.Story, WorkItemType.Bug, WorkItemType.Story, WorkItemType.Epic], rows.Select(r => r.Type));
        var story = rows.Single(r => r.Key == "WEB-2");
        Assert.Equal((new ContainerView("WEB-1", "Card refunds", WorkItemType.Epic, EpicColor.Purple), 3m), (story.Container, story.Estimate));
        Assert.Null(rows.Single(r => r.Key == "WEB-4").Container);
    }

    [Fact]
    public async Task P3_US1_AS8_The_list_filters_by_type_and_by_epic()
    {
        await TypesSetUpAsync();

        Assert.Equal(Keys(3), await KeysAsync(new WorkItemListQuery(Types: [WorkItemType.Bug], ContainerKeys: ["WEB-1"])));
        Assert.Equal(Keys(4, 2), await KeysAsync(new WorkItemListQuery(Types: [WorkItemType.Story])));
        Assert.Equal(Keys(3, 2), await KeysAsync(new WorkItemListQuery(ContainerKeys: ["WEB-1"])));
        Assert.Equal(Keys(5, 4, 1), await KeysAsync(new WorkItemListQuery(NoContainer: true)));
        Assert.Equal(Keys(4, 3, 2), await KeysAsync(new WorkItemListQuery(ContainerKeys: ["WEB-1"], NoContainer: true, Types: [WorkItemType.Story, WorkItemType.Bug])));
    }

    [Fact]
    public async Task P3_The_list_sorts_by_type_and_carries_the_template()
    {
        await TypesSetUpAsync();

        var view = await ListAsync(new WorkItemListQuery(ListSort.Type, Descending: false));

        Assert.Equal(ProjectTemplate.Kanban, view.Template);
        Assert.Equal(["Bug", "Epic", "Story", "Story", "Sub-task"], view.Rows.Items.Select(r => WorkItemTypes.DisplayName(r.Type)));
    }

    [Fact]
    public async Task P4_US4_AS6_Rows_carry_labels_and_the_list_filters_by_any_of_several()
    {
        await TaskAsync("Launch page");
        await TaskAsync("Pricing table");
        await TaskAsync("Footer");
        await LabelAsync("WEB-1", "launch");
        await LabelAsync("WEB-1", "design");
        await LabelAsync("WEB-2", "Pricing");

        Assert.Equal(["design", "launch"], (await ListAsync()).Rows.Items.Single(r => r.Key == "WEB-1").Labels);
        Assert.Equal(Keys(1), await KeysAsync(new WorkItemListQuery(Labels: ["LAUNCH"])));
        Assert.Equal(Keys(2, 1), await KeysAsync(new WorkItemListQuery(Labels: ["launch", "pricing"])));
        Assert.Empty(await KeysAsync(new WorkItemListQuery(Labels: ["nobody-uses-this"])));
        Assert.Equal(Keys(1), await KeysAsync(new WorkItemListQuery(Labels: ["design"], Text: "launch")));
    }

    private async Task LabelAsync(string key, string label) =>
        (await CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync(key, label, Ct))).ValueOrThrow();
}
