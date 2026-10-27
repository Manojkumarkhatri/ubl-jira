using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Sub-tasks are cards on the board, as in Jira (Jira parity pass US5 scenario 6, FR-027a; research R18).</summary>
public sealed class BoardSubtaskCardsTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static CardView Card(BoardView board, string key) => board.Columns.SelectMany(c => c.Cards).Single(c => c.Key == key);

    private async Task<string> SubtaskAsync(string parentKey, string title) =>
        (await AddSubtaskAsync(parentKey, title)).Subtasks.Items[^1].Key;

    [Fact]
    public async Task JP_US5_AS6_Sub_tasks_are_cards_in_their_status_column_with_their_parent_and_its_epic()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");                  // WEB-1
        await CreateAsync(WorkItemType.Story, "Refund to card", "WEB-1");      // WEB-2
        var subtask = await SubtaskAsync("WEB-2", "Write the refund API");     // WEB-3
        (await EditAsync(subtask, new WorkItemEdit.Status(InProgress))).ValueOrThrow();

        var board = await BoardAsync();

        var card = Card(board, subtask);
        Assert.Equal((InProgress, WorkItemType.Subtask, "WEB-2", "WEB-1"), (card.ColumnId, card.Type, card.ParentKey, card.Container?.Key));
        Assert.Equal(["WEB-2"], board.Columns[0].Cards.Select(c => c.Key));
        Assert.Equal((1, 0), (Card(board, "WEB-2").SubtasksTotal, Card(board, "WEB-2").SubtasksDone));
    }

    [Fact]
    public async Task In_a_column_sub_tasks_follow_their_parent_then_their_own_order()
    {
        var first = await AddAsync(ToDo, "Refund to card");   // WEB-1
        var second = await AddAsync(ToDo, "Refund to wallet"); // WEB-2
        var b1 = await SubtaskAsync(second.Key, "API");        // WEB-3
        var b2 = await SubtaskAsync(second.Key, "Screens");    // WEB-4
        var a1 = await SubtaskAsync(first.Key, "Ledger");      // WEB-5

        Assert.Equal([first.Key, a1, second.Key, b1, b2], await KeysInAsync(ToDo));
    }

    [Fact]
    public async Task Column_counts_and_the_work_in_progress_limit_include_sub_tasks()
    {
        var card = await AddAsync(InProgress, "Refund to card");
        await SubtaskAsync(card.Key, "API");
        await SubtaskAsync(card.Key, "Screens");
        var columns = (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.GetAsync("WEB", Ct))).ValueOrThrow();
        (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s =>
            s.SetWipLimitAsync("WEB", InProgress, 2, columns.BoardVersion, Ct))).ValueOrThrow();
        foreach (var key in new[] { "WEB-2", "WEB-3" })
        {
            (await EditAsync(key, new WorkItemEdit.Status(InProgress))).ValueOrThrow();
        }

        var column = (await BoardAsync()).Columns.Single(c => c.Id == InProgress);

        Assert.Equal((3, true), (column.CardCount, column.OverLimit));
    }

    [Fact]
    public async Task Done_sub_tasks_follow_the_14_day_window()
    {
        var card = await AddAsync(ToDo, "Refund to card");
        var subtask = await SubtaskAsync(card.Key, "API");
        (await EditAsync(subtask, new WorkItemEdit.Status(Done))).ValueOrThrow();
        Assert.Contains(subtask, await KeysInAsync(Done));

        Harness.Time.Advance(TimeSpan.FromDays(15));

        var board = await BoardAsync();
        Assert.DoesNotContain(subtask, board.Columns.SelectMany(c => c.Cards).Select(c => c.Key));
        Assert.Equal(1, board.HiddenDoneCount);
        Assert.Contains(subtask, await KeysInAsync(Done, showAllDone: true));
    }

    [Fact]
    public async Task Moving_a_sub_task_card_changes_its_column_but_not_its_order()
    {
        var card = await AddAsync(ToDo, "Refund to card");
        var first = await SubtaskAsync(card.Key, "API");
        var second = await SubtaskAsync(card.Key, "Screens");
        var subtaskCard = Card(await BoardAsync(), second);

        var moved = await MoveAsync(subtaskCard, InProgress, CardPlacement.AtTop);

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal((InProgress, card.Key), (moved.Value!.ColumnId, moved.Value.ParentKey));
        Assert.Equal([second], await KeysInAsync(InProgress));
        var back = await MoveAsync(moved.Value, ToDo, new CardPlacement.Before(card.Key));
        Assert.True(back.IsSuccess, back.Error?.Message);
        Assert.Equal([card.Key, first, second], await KeysInAsync(ToDo));
    }

    [Fact]
    public async Task A_deleted_epics_items_and_their_sub_tasks_show_no_epic()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");             // WEB-1
        await CreateAsync(WorkItemType.Story, "Refund to card", "WEB-1"); // WEB-2
        var subtask = await SubtaskAsync("WEB-2", "API");                 // WEB-3

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).IsSuccess);

        var board = await BoardAsync();
        Assert.Null(Card(board, "WEB-2").Container);
        Assert.Null(Card(board, subtask).Container);
    }
}

/// <summary>On Scrum boards, only the sub-tasks of cards in the active sprint (Jira parity pass FR-027a).</summary>
public sealed class ScrumBoardSubtaskCardsTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    [Fact]
    public async Task Only_sub_tasks_of_cards_in_the_active_sprint_are_shown()
    {
        var inSprint = await StoryAsync("Refund to card");
        var inBacklog = await StoryAsync("Refund to wallet");
        var sprint = await NewSprintAsync();
        await MoveAsync(inSprint, Into(sprint));
        await StartAsync(sprint.Id, October5);
        var shown = (await AddSubtaskAsync(inSprint, "API")).Subtasks.Items[0].Key;
        var hidden = (await AddSubtaskAsync(inBacklog, "Screens")).Subtasks.Items[0].Key;

        var keys = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Select(c => c.Key).ToList();

        Assert.Equal([inSprint, shown], keys);
        Assert.DoesNotContain(hidden, keys);
    }
}

/// <summary>A sub-task card in a locked stage cannot start, on the board as in the drawer (Phase 3 FR-032; research R18).</summary>
public sealed class StageGateSubtaskCardsTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    [Fact]
    public async Task A_sub_task_card_of_work_in_a_locked_stage_cannot_leave_to_do()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");
        var subtask = (await AddSubtaskAsync(task, "List the tables")).Subtasks.Items[0].Key;
        var card = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == subtask);

        var moved = await MoveAsync(card, InProgress, CardPlacement.AtEnd);

        Assert.Equal(ErrorCodes.StageLocked, moved.Error!.Code);
        Assert.Equal((Planning, ProjectTemplate.StageGate), (card.Container?.Key, (await BoardAsync()).Template));
    }
}
