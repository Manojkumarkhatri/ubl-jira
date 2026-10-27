using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Work in a locked stage cannot start, on any path that changes a status, for anyone (Phase 3 FR-032–FR-034;
/// research R15; SC-005).</summary>
public sealed class StageLockTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    [Fact]
    public async Task P3_US3_AS2_Work_in_a_locked_stage_cannot_leave_to_do_on_the_board_or_in_the_drawer()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");
        var card = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == task);

        foreach (var user in new[] { Musa, Owner, await Data.AdministratorAsync() })
        {
            ActAs(user);
            var moved = await MoveAsync(card, InProgress, CardPlacement.AtEnd);
            Assert.Equal(ErrorCodes.StageLocked, moved.Error!.Code);
            Assert.Contains("Initiation", moved.Error.Message, StringComparison.Ordinal);
            Assert.Equal(ErrorCodes.StageLocked, (await StartAsync(task)).Error!.Code);
        }

        ActAs(Musa);
        (await EditAsync(task, new WorkItemEdit.Title("Plan the data migration"))).ValueOrThrow();
        (await EditAsync(task, new WorkItemEdit.Assignee(Musa.Id))).ValueOrThrow();
        (await EditAsync(task, new WorkItemEdit.Dates(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 20)))).ValueOrThrow();
        Assert.Equal(ToDo, (await RequireDetailsAsync(task)).Status.Id);
    }

    [Fact]
    public async Task P3_US3_AS2_A_sub_task_of_locked_work_cannot_be_marked_done()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");
        var subtask = (await AddSubtaskAsync(task, "List the tables")).Subtasks.Items[0];

        var result = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.MarkSubtaskDoneAsync(subtask.Key, subtask.Version, Ct));

        Assert.Equal(ErrorCodes.StageLocked, result.Error!.Code);
        Assert.Equal(ErrorCodes.StageLocked, (await EditAsync(subtask.Key, new WorkItemEdit.Status(Done))).Error!.Code);
    }

    [Fact]
    public async Task Work_unlocks_when_the_earlier_gate_is_approved()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");

        await ApproveAsync(Initiation);

        Assert.Equal(InProgress, (await StartAsync(task)).ValueOrThrow().Status.Id);
        Assert.Null((await RequireDetailsAsync(task)).LockedUntil);
    }

    [Fact]
    public async Task The_drawer_names_the_gate_that_locks_the_work()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");

        Assert.Equal("Initiation", (await RequireDetailsAsync(task)).LockedUntil);
        Assert.Null((await RequireDetailsAsync(await TaskInAsync(Initiation, "Write the charter"))).LockedUntil);
    }

    [Fact]
    public async Task P3_FR033_Started_work_cannot_be_put_into_a_locked_stage()
    {
        var started = (await CreateAsync(WorkItemType.Task, "Set up the servers")).Key;
        (await StartAsync(started)).ValueOrThrow();

        Assert.Equal(ErrorCodes.StageLocked, (await EditAsync(started, new WorkItemEdit.Container(Planning))).Error!.Code);
        (await EditAsync(started, new WorkItemEdit.Container(Initiation))).ValueOrThrow();
    }

    [Fact]
    public async Task P3_FR032_Items_in_no_stage_are_never_locked()
    {
        var loose = (await CreateAsync(WorkItemType.Task, "Order the laptops")).Key;

        Assert.Equal(InProgress, (await StartAsync(loose)).ValueOrThrow().Status.Id);
    }

    [Fact]
    public async Task P3_US3_AS7_A_stage_is_completed_only_after_its_gate_is_approved()
    {
        Assert.Equal(ErrorCodes.GateNotApproved, (await EditAsync(Initiation, new WorkItemEdit.Status(Done))).Error!.Code);

        await ApproveAsync(Initiation);

        Assert.Equal(Done, (await EditAsync(Initiation, new WorkItemEdit.Status(Done))).ValueOrThrow().Status.Id);
    }

    [Fact]
    public async Task A_locked_stage_itself_cannot_start()
    {
        Assert.Equal(ErrorCodes.StageLocked, (await StartAsync(Planning)).Error!.Code);
        Assert.Equal(InProgress, (await StartAsync(Initiation)).ValueOrThrow().Status.Id);
    }

    [Fact]
    public async Task P3_FR033_Deleting_a_column_that_holds_locked_work_sends_it_only_to_a_to_do_column()
    {
        await TaskInAsync(Planning, "Plan the migration");
        var board = await BoardAsync();
        var added = (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s =>
            s.AddAsync("WEB", "Backlog", StatusCategory.ToDo, 0, board.BoardVersion, Ct))).ValueOrThrow();
        var backlog = added.Columns.Single(c => c.Name == "Backlog").Id;

        var refused = await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s =>
            s.DeleteAsync("WEB", ToDo, InProgress, added.BoardVersion, Ct));
        Assert.Equal(ErrorCodes.StageLocked, refused.Error!.Code);

        (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.DeleteAsync("WEB", ToDo, backlog, added.BoardVersion, Ct))).ValueOrThrow();
    }
}
