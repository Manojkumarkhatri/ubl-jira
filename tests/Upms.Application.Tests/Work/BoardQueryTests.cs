using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Reading the board (FR-017, FR-020, FR-021).</summary>
public sealed class BoardQueryTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    [Fact]
    public async Task Columns_come_in_position_order_with_cards_in_rank_order_and_counts()
    {
        await AddAsync(ToDo, "First");
        await AddAsync(ToDo, "Second");
        await AddAsync(InProgress, "Third");

        var board = await BoardAsync();

        Assert.Equal(["To Do", "In Progress", "Done"], board.Columns.Select(c => c.Name));
        Assert.Equal(["First", "Second"], board.Columns[0].Cards.Select(c => c.Title));
        Assert.Equal([2, 1, 0], board.Columns.Select(c => c.CardCount));
        Assert.All(board.Columns[0].Cards, c => Assert.Equal(Upms.Domain.Work.Priority.Medium, c.Priority));
        Assert.True(board.CanManageColumns);
    }

    [Fact]
    public async Task P4_US4_AS4_Cards_carry_their_labels_and_their_epic_or_stage()
    {
        await AddAsync(ToDo, "Plain");
        var labelled = await AddAsync(ToDo, "Labelled");
        await CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync(labelled.Key, "launch", Ct));
        await CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync(labelled.Key, "Design", Ct));

        var cards = (await BoardAsync()).Columns[0].Cards;

        Assert.Equal([[], ["Design", "launch"]], cards.Select(c => c.Labels.ToArray()));
        Assert.All(cards, c => Assert.Null(c.Container)); // a Kanban board has no epics; Scrum and Stage-gate cards carry theirs
    }

    [Fact]
    public async Task US1_AS10_Done_columns_show_tasks_completed_in_the_last_14_days_unless_all_are_requested()
    {
        var old = await AddAsync(ToDo, "Finished long ago");
        await MoveAsync(old, Done, CardPlacement.AtEnd);
        Harness.Time.Advance(TimeSpan.FromDays(20));
        var recent = await AddAsync(ToDo, "Finished yesterday");
        await MoveAsync(recent, Done, CardPlacement.AtEnd);

        var board = await BoardAsync();
        var doneColumn = board.Columns[2];
        Assert.Equal(["Finished yesterday"], doneColumn.Cards.Select(c => c.Title));
        Assert.Equal(1, board.HiddenDoneCount);

        var all = await BoardAsync(showAllDone: true);
        Assert.Equal(["Finished long ago", "Finished yesterday"], all.Columns[2].Cards.Select(c => c.Title));
        Assert.Equal(0, all.HiddenDoneCount);
    }

    [Fact]
    public async Task Every_user_sees_the_same_board()
    {
        await AddAsync(ToDo, "One");
        await AddAsync(ToDo, "Two");
        var mine = await BoardAsync();

        ActAs(await MemberAsync("bilal"));
        var theirs = await BoardAsync();

        Assert.Equal(mine.Columns.SelectMany(c => c.Cards.Select(x => x.Key)), theirs.Columns.SelectMany(c => c.Cards.Select(x => x.Key)));
        Assert.False(theirs.CanManageColumns);
    }

    [Fact]
    public async Task P2_Cards_carry_the_assignee_and_due_date_and_the_board_knows_its_viewer()
    {
        var bilal = await MemberAsync("bilal");
        var assigned = await AddAsync(ToDo, "Assigned and due");
        await AddAsync(ToDo, "Nothing set");
        var version = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.UpdateAsync(assigned.Key, new WorkItemEdit.Assignee(bilal.Id), assigned.Version, Ct))).ValueOrThrow().Version;
        await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.UpdateAsync(assigned.Key, new WorkItemEdit.Dates(null, new DateOnly(2026, 10, 10)), version, Ct));

        ActAs(bilal);
        var board = await BoardAsync();

        var cards = board.Columns[0].Cards;
        Assert.Equal(new AssigneeRef(bilal.Id, "Bilal Tester", "BT", CanWork: true), cards[0].Assignee);
        Assert.Equal(new DateOnly(2026, 10, 10), cards[0].DueDate);
        Assert.Equal((null, (DateOnly?)null), (cards[1].Assignee, cards[1].DueDate));
        Assert.Equal(bilal.Id, board.ViewerId);
    }

    [Fact]
    public async Task Unknown_projects_are_not_found()
    {
        var result = await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("NOPE", false, Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task P3_FR013_Epics_are_not_cards_and_cards_carry_their_type_and_estimate()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");                  // WEB-1
        var story = await CreateAsync(WorkItemType.Story, "Refund to card");   // WEB-2
        await AddAsync(ToDo, "Refund shows twice", WorkItemType.Bug);          // WEB-3
        await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.UpdateAsync(story.Key, new WorkItemEdit.Estimate(5), story.Version, Ct));

        var board = await BoardAsync();

        Assert.Equal(ProjectTemplate.Kanban, board.Template);
        var cards = board.Columns[0].Cards;
        Assert.Equal(["WEB-2", "WEB-3"], cards.Select(c => c.Key));
        Assert.Equal([(WorkItemType.Story, (decimal?)5m), (WorkItemType.Bug, null)], cards.Select(c => (c.Type, c.Estimate)));
        Assert.Equal(2, board.Columns[0].CardCount);
    }
}

/// <summary>A Scrum board shows the active sprint (Phase 3 FR-027, research R12).</summary>
public sealed class ScrumBoardTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    [Fact]
    public async Task P3_US2_AS9_Without_an_active_sprint_the_board_has_no_cards()
    {
        await StoryAsync("Refund to card");
        await NewSprintAsync();

        var board = await BoardAsync();

        Assert.Null(board.Sprint);
        Assert.All(board.Columns, c => Assert.Empty(c.Cards));
        Assert.True(board.CanManageSprints);
    }

    [Fact]
    public async Task P4_US4_AS7_Cards_carry_their_epic_for_the_quick_filters()
    {
        var epic = await CreateAsync(WorkItemType.Epic, "Card refunds");
        var inEpic = (await CreateAsync(WorkItemType.Bug, "Refund fails", epic.Key)).Key;
        var alone = await StoryAsync("Refund history");
        var sprint = await NewSprintAsync();
        await MoveAsync(inEpic, Into(sprint));
        await MoveAsync(alone, Into(sprint));
        await StartAsync(sprint.Id);

        var cards = (await BoardAsync()).Columns.Single(c => c.Id == ToDo).Cards;

        Assert.Equal(new ContainerView(epic.Key, "Card refunds", WorkItemType.Epic, EpicColor.Purple), cards.Single(c => c.Key == inEpic).Container);
        Assert.Null(cards.Single(c => c.Key == alone).Container);
    }

    [Fact]
    public async Task P3_US2_AS5_The_board_shows_the_active_sprint_with_all_of_its_done_cards()
    {
        var old = await StoryAsync("Done long ago");
        var open = await StoryAsync("Still open");
        await StoryAsync("In the backlog");
        var sprint = await NewSprintAsync();
        var next = await NewSprintAsync();
        await MoveAsync(old, Into(sprint));
        await MoveAsync(open, Into(sprint));
        await StartAsync(sprint.Id, October5, goal: "Ship refunds");
        await MarkDoneAsync(old);
        Harness.Time.Advance(TimeSpan.FromDays(20));   // past the 14 days a Kanban board shows done cards for

        var board = await BoardAsync();

        Assert.Equal([open], board.Columns.Single(c => c.Id == ToDo).Cards.Select(c => c.Key));
        Assert.Equal([old], board.Columns.Single(c => c.Id == Done).Cards.Select(c => c.Key));
        Assert.Equal(0, board.HiddenDoneCount);
        var info = board.Sprint!;
        Assert.Equal(("WEB Sprint 1", "Ship refunds", October5, October5.AddDays(14)), (info.Name, info.Goal, info.StartDate, info.EndDate));
        Assert.Equal([(next.Id, "WEB Sprint 2")], info.Destinations.Select(d => (d.Id, d.Name)));
    }

    [Fact]
    public async Task P3_FR027_A_card_created_on_the_board_joins_the_active_sprint()
    {
        var sprint = await NewSprintAsync();
        await StartAsync(sprint.Id);

        var card = await AddAsync(ToDo, "Found during the sprint", WorkItemType.Bug);

        Assert.Equal([card.Key], KeysOf((await SprintAsync(sprint.Id)).Items));
        Assert.Equal([(card.Key, SprintScopeKind.Added, (decimal?)null)], await ScopeEventsAsync(sprint.Id));
        Assert.Equal([card.Key], await KeysInAsync(ToDo));
    }

    [Fact]
    public async Task P3_FR027_Cards_created_anywhere_else_go_to_the_bottom_of_the_backlog()
    {
        var sprint = await NewSprintAsync();
        await StartAsync(sprint.Id);
        var first = await StoryAsync("From the list");

        var second = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Task, "From the backlog"), Ct))).ValueOrThrow().Key;

        Assert.Equal([first, second], await BacklogKeysAsync());
        Assert.Empty((await SprintAsync(sprint.Id)).Items);
    }
}
