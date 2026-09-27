using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Work;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Projects;

/// <summary>Board columns (FR-034 to FR-041, SC-006).</summary>
public sealed class BoardColumnServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private async Task<BoardColumnsView> ColumnsAsync() =>
        (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.GetAsync("WEB", Ct))).ValueOrThrow();

    private Task<Result<BoardColumnsView>> ChangeAsync(Func<IBoardColumnService, Task<Result<BoardColumnsView>>> change) =>
        CallAsync(change);

    private async Task<BoardColumnsView> AddColumnAsync(string name, StatusCategory category, int position)
    {
        var version = (await ColumnsAsync()).BoardVersion;
        return (await ChangeAsync(s => s.AddAsync("WEB", name, category, position, version, Ct))).ValueOrThrow();
    }

    private static long IdOf(BoardColumnsView view, string name) => view.Columns.Single(c => c.Name == name).Id;

    private Task<long> StatusOfAsync(string key) =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters().Where(w => w.Key == key).Select(w => w.StatusId).SingleAsync(Ct));

    [Fact]
    public async Task US3_AS1_An_added_column_appears_in_its_position_for_everyone()
    {
        var view = await AddColumnAsync("In Review", StatusCategory.InProgress, 2);

        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], view.Columns.Select(c => c.Name));
        Assert.Equal(Board.BoardVersion + 1, view.BoardVersion);
        Assert.True(view.Columns[2].IsEmpty);

        ActAs(await Data.UserAsync("bilal"));
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], (await BoardAsync()).Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task US3_AS2_Renaming_a_column_keeps_its_tasks_in_place_and_renames_their_status()
    {
        var card = await AddAsync(ToDo, "Design the home page");

        var view = (await ChangeAsync(s => s.RenameAsync("WEB", ToDo, "Backlog", Board.BoardVersion, Ct))).ValueOrThrow();

        Assert.Equal("Backlog", view.Columns[0].Name);
        var board = await BoardAsync();
        Assert.Equal("Backlog", board.Columns[0].Name);
        Assert.Equal([card.Key], board.Columns[0].Cards.Select(c => c.Key));
        Assert.Equal("Backlog", (await RequireDetailsAsync(card.Key)).Status.Name);
    }

    [Fact]
    public async Task US3_AS3_A_new_column_order_is_kept()
    {
        await ChangeAsync(s => s.MoveAsync("WEB", Done, 0, Board.BoardVersion, Ct));

        Assert.Equal(["Done", "To Do", "In Progress"], (await ColumnsAsync()).Columns.Select(c => c.Name));
        Assert.Equal(["Done", "To Do", "In Progress"], (await BoardAsync()).Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task US3_AS5_Deleting_a_column_moves_all_its_work_items_to_the_destination_and_records_each_move()
    {
        var review = IdOf(await AddColumnAsync("In Review", StatusCategory.InProgress, 2), "In Review");
        var existing = await AddAsync(Done, "Register the domain");                  // WEB-1
        var first = await AddAsync(review, "Build the header");                       // WEB-2
        var second = await AddAsync(review, "Build the footer");                      // WEB-3
        var subtask = (await AddSubtaskAsync(first.Key, "Logo")).Subtasks.Items[0];   // WEB-4, in To Do
        Assert.True((await EditAsync(subtask.Key, new WorkItemEdit.Status(review))).IsSuccess);
        var deleted = await AddAsync(review, "Old idea");                             // WEB-5
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(deleted.Key, Ct))).IsSuccess);
        var before = await ColumnsAsync();

        var view = (await ChangeAsync(s => s.DeleteAsync("WEB", review, Done, before.BoardVersion, Ct))).ValueOrThrow();

        Assert.Equal(["To Do", "In Progress", "Done"], view.Columns.Select(c => c.Name));
        Assert.Equal(before.BoardVersion + 1, view.BoardVersion);
        Assert.Equal([existing.Key, first.Key, second.Key], await KeysInAsync(Done)); // appended in their order
        foreach (var key in new[] { first.Key, second.Key, subtask.Key, deleted.Key })
        {
            Assert.Equal(Done, await StatusOfAsync(key));
        }

        var details = await RequireDetailsAsync(first.Key);
        Assert.NotNull(details.ResolvedAt); // moved into a "done" column: completed
        var move = Assert.Single((await HistoryAsync(first.Key)).Items, h => h.Note == "column deleted");
        Assert.Equal((WorkItemField.Status, "In Review", "Done"), (move.Field, move.OldValue, move.NewValue));
        Assert.Contains((await HistoryAsync(subtask.Key)).Items, h => h.Note == "column deleted");
    }

    [Fact]
    public async Task Moving_work_items_out_of_a_done_column_reopens_them()
    {
        var archived = IdOf(await AddColumnAsync("Archived", StatusCategory.Done, 3), "Archived");
        var card = await AddAsync(archived, "Launch the site");
        Assert.NotNull((await RequireDetailsAsync(card.Key)).ResolvedAt);
        var version = (await ColumnsAsync()).BoardVersion;

        Assert.True((await ChangeAsync(s => s.DeleteAsync("WEB", archived, ToDo, version, Ct))).IsSuccess);

        var details = await RequireDetailsAsync(card.Key);
        Assert.Equal(("To Do", (DateTimeOffset?)null), (details.Status.Name, details.ResolvedAt));
    }

    [Fact]
    public async Task Deleting_a_column_that_holds_work_items_requires_a_destination()
    {
        await AddAsync(InProgress, "Set up hosting");

        var result = await ChangeAsync(s => s.DeleteAsync("WEB", InProgress, null, Board.BoardVersion, Ct));

        Assert.Equal(ErrorCodes.DestinationRequired, result.Error!.Code);
        Assert.Equal(3, (await ColumnsAsync()).Columns.Count);
    }

    [Fact]
    public async Task An_empty_column_is_deleted_without_a_destination()
    {
        var view = (await ChangeAsync(s => s.DeleteAsync("WEB", InProgress, null, Board.BoardVersion, Ct))).ValueOrThrow();

        Assert.Equal(["To Do", "Done"], view.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task US3_AS6_The_last_to_do_and_done_columns_cannot_be_deleted_or_retyped()
    {
        var deleteToDo = await ChangeAsync(s => s.DeleteAsync("WEB", ToDo, InProgress, Board.BoardVersion, Ct));
        var retypeDone = await ChangeAsync(s => s.ChangeCategoryAsync("WEB", Done, StatusCategory.InProgress, Board.BoardVersion, Ct));

        Assert.Equal((ErrorKind.RuleViolation, ErrorCodes.LastToDoColumn), (deleteToDo.Error!.Kind, deleteToDo.Error.Code));
        Assert.Equal(ErrorCodes.LastDoneColumn, retypeDone.Error!.Code);
        Assert.Contains("only", retypeDone.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Board.BoardVersion, (await ColumnsAsync()).BoardVersion);
    }

    [Fact]
    public async Task A_columns_type_changes_only_while_no_work_item_has_it_deleted_ones_included()
    {
        var card = await AddAsync(InProgress, "Set up hosting");
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(card.Key, Ct))).IsSuccess);

        var view = await ColumnsAsync();
        var refused = await ChangeAsync(s => s.ChangeCategoryAsync("WEB", InProgress, StatusCategory.ToDo, view.BoardVersion, Ct));
        var allowed = await ChangeAsync(s => s.ChangeCategoryAsync("WEB", ToDo, StatusCategory.InProgress, view.BoardVersion, Ct));

        Assert.Equal((0, false), (view.Columns[1].ItemCount, view.Columns[1].IsEmpty));
        Assert.Equal(ErrorCodes.ColumnNotEmpty, refused.Error!.Code);
        Assert.Equal(ErrorCodes.LastToDoColumn, allowed.Error!.Code); // To Do is empty but the only "to do" column
        await AddColumnAsync("Ideas", StatusCategory.ToDo, 0);
        var version = (await ColumnsAsync()).BoardVersion;
        Assert.True((await ChangeAsync(s => s.ChangeCategoryAsync("WEB", ToDo, StatusCategory.InProgress, version, Ct))).IsSuccess);
    }

    [Fact]
    public async Task US3_AS7_A_name_already_on_the_board_is_refused_ignoring_case()
    {
        var added = await ChangeAsync(s => s.AddAsync("WEB", "done", StatusCategory.Done, 3, Board.BoardVersion, Ct));
        var renamed = await ChangeAsync(s => s.RenameAsync("WEB", InProgress, "TO DO", Board.BoardVersion, Ct));

        Assert.Equal(ErrorCodes.DuplicateColumnName, added.Error!.Code);
        Assert.Equal(ErrorCodes.DuplicateColumnName, renamed.Error!.Code);
    }

    [Fact]
    public async Task Invalid_names_and_limits_are_validation_errors_for_their_field()
    {
        var name = await ChangeAsync(s => s.AddAsync("WEB", new string('x', 31), StatusCategory.InProgress, 1, Board.BoardVersion, Ct));
        var limit = await ChangeAsync(s => s.SetWipLimitAsync("WEB", InProgress, 100, Board.BoardVersion, Ct));

        Assert.Equal(ErrorKind.Validation, name.Error!.Kind);
        Assert.True(name.Error.FieldErrors!.ContainsKey("Name"));
        Assert.True(limit.Error!.FieldErrors!.ContainsKey("WipLimit"));
    }

    [Fact]
    public async Task A_board_holds_at_most_ten_columns()
    {
        for (var i = 1; i <= 7; i++)
        {
            await AddColumnAsync($"Step {i}", StatusCategory.InProgress, 2);
        }

        var version = (await ColumnsAsync()).BoardVersion;
        var result = await ChangeAsync(s => s.AddAsync("WEB", "Eleventh", StatusCategory.InProgress, 2, version, Ct));

        Assert.Equal(ErrorCodes.TooManyColumns, result.Error!.Code);
    }

    [Fact]
    public async Task A_stale_board_version_returns_conflict_with_the_current_columns()
    {
        var first = await ChangeAsync(s => s.RenameAsync("WEB", ToDo, "Backlog", Board.BoardVersion, Ct));
        var second = await ChangeAsync(s => s.AddAsync("WEB", "Blocked", StatusCategory.InProgress, 1, Board.BoardVersion, Ct));

        Assert.True(first.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, second.Error!.Kind);
        var current = Assert.IsType<BoardColumnsView>(second.Error.Current);
        Assert.Equal(["Backlog", "In Progress", "Done"], current.Columns.Select(c => c.Name));
        Assert.Equal(first.Value!.BoardVersion, current.BoardVersion);
    }

    [Fact]
    public async Task Unknown_columns_are_not_found()
    {
        var result = await ChangeAsync(s => s.RenameAsync("WEB", 999_999, "Anything", Board.BoardVersion, Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task A_destination_on_another_board_is_refused()
    {
        var other = await Data.ProjectAsync("MOB", Owner.Id);
        var otherColumn = other.Statuses[0].Id;
        await AddAsync(InProgress, "Set up hosting");

        var result = await ChangeAsync(s => s.DeleteAsync("WEB", InProgress, otherColumn, Board.BoardVersion, Ct));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(3, (await ColumnsAsync()).Columns.Count);
    }

    [Fact]
    public async Task US3_AS8_Only_the_owner_and_administrators_change_columns_but_everyone_keeps_working_on_tasks()
    {
        var bilal = await Data.UserAsync("bilal");
        ActAs(bilal);

        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.GetAsync("WEB", Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await ChangeAsync(s => s.AddAsync("WEB", "QA", StatusCategory.InProgress, 2, Board.BoardVersion, Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await ChangeAsync(s => s.RenameAsync("WEB", ToDo, "Backlog", Board.BoardVersion, Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await ChangeAsync(s => s.DeleteAsync("WEB", InProgress, null, Board.BoardVersion, Ct))).Error!.Kind);
        Assert.False((await BoardAsync()).CanManageColumns);
        var card = await AddAsync(ToDo, "Bilal's task");
        Assert.True((await MoveAsync(card, InProgress, CardPlacement.AtEnd)).IsSuccess);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await ChangeAsync(s => s.RenameAsync("WEB", ToDo, "Backlog", Board.BoardVersion, Ct))).IsSuccess);
    }
}
