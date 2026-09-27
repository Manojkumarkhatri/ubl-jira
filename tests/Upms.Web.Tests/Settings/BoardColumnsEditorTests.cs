using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Pages.Settings;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Settings;

/// <summary>The board column settings (FR-034 to FR-041).</summary>
public sealed class BoardColumnsEditorTests : BunitTestBase
{
    private readonly FakeBoardColumnService _columns = new();

    public BoardColumnsEditorTests()
    {
        Services.AddSingleton<IBoardColumnService>(_columns);
        Services.AddScoped<LiveAnnouncer>();
    }

    private IRenderedComponent<BoardColumnsEditor> RenderEditor() => Render<BoardColumnsEditor>(p => p.Add(x => x.ProjectKey, "WEB"));

    private static IElement Row(IRenderedComponent<BoardColumnsEditor> cut, int index) => cut.FindAll("[data-testid=column-row]")[index];

    private static IElement Button(IElement row, string text) =>
        row.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Columns_are_listed_left_to_right_with_type_limit_and_work_item_count()
    {
        var cut = RenderEditor();

        var rows = cut.FindAll("[data-testid=column-row]");
        Assert.Equal(["To Do", "In Progress", "Done"], rows.Select(r => r.QuerySelector(".column-name")!.TextContent));
        Assert.Equal("ToDo", cut.Find("#column-1-type").GetAttribute("value"));
        Assert.Equal("3", cut.Find("#column-2-limit").GetAttribute("value"));
        Assert.Contains("2 work items", rows[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US3_AS1_A_column_is_added_at_the_chosen_position()
    {
        var cut = RenderEditor();
        Assert.Equal("2", cut.Find("#new-column-position").GetAttribute("value")); // before Done by default

        cut.Find("#new-column-name").Input("In Review");
        cut.Find("#new-column-type").Change("InProgress");
        cut.Find("form.add-column").Submit();

        Assert.Equal(["add In Review InProgress 2 v7"], _columns.Calls);
        Assert.Equal(4, cut.FindAll("[data-testid=column-row]").Count);
        Assert.Contains("Added the column In Review", cut.Find("[data-testid=columns-saved]").TextContent, StringComparison.Ordinal);
        Assert.Equal("", cut.Find("#new-column-name").GetAttribute("value"));
    }

    [Fact]
    public void US3_AS7_A_refused_name_keeps_the_input_and_shows_why()
    {
        _columns.NextResult = () => AppError.Rule(ErrorCodes.DuplicateColumnName, "This board already has a column named Done.");
        var cut = RenderEditor();

        cut.Find("#new-column-name").Input("done");
        cut.Find("form.add-column").Submit();

        Assert.Equal("done", cut.Find("#new-column-name").GetAttribute("value"));
        Assert.Contains("already has a column named Done", cut.Find("#new-column-name-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US3_AS2_A_column_is_renamed_inline()
    {
        var cut = RenderEditor();

        Button(Row(cut, 0), "Rename").Click();
        cut.Find("#column-1-name-input").Input("Backlog");
        cut.Find("#column-1-name-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["rename 1 Backlog v7"], _columns.Calls);
        Assert.Equal("Backlog", Row(cut, 0).QuerySelector(".column-name")!.TextContent);
    }

    [Fact]
    public void US3_AS3_Columns_move_left_and_right_and_the_ends_are_disabled()
    {
        var cut = RenderEditor();

        Assert.True(Button(Row(cut, 0), "Move left").HasAttribute("disabled"));
        Assert.True(Button(Row(cut, 2), "Move right").HasAttribute("disabled"));
        Button(Row(cut, 0), "Move right").Click();

        Assert.Equal(["move 1 1 v7"], _columns.Calls);
        Assert.Equal(["In Progress", "To Do", "Done"], cut.FindAll(".column-name").Select(n => n.TextContent));
    }

    [Fact]
    public void Dropping_a_column_on_another_moves_it_to_that_position()
    {
        var cut = RenderEditor();

        Row(cut, 2).DragStart();
        Row(cut, 0).Drop();

        Assert.Equal(["move 3 0 v7"], _columns.Calls);
        Assert.Equal(["Done", "To Do", "In Progress"], cut.FindAll(".column-name").Select(n => n.TextContent));
    }

    [Fact]
    public void The_type_is_disabled_with_a_hint_while_the_column_holds_work_items()
    {
        var cut = RenderEditor();

        Assert.True(cut.Find("#column-1-type").HasAttribute("disabled"));
        Assert.Contains("Only an empty column can change type", Row(cut, 0).TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find("#column-3-type").HasAttribute("disabled"));
        cut.Find("#column-3-type").Change("InProgress");

        Assert.Equal(["type 3 InProgress v7"], _columns.Calls);
    }

    [Fact]
    public void A_refused_type_change_explains_why()
    {
        _columns.NextResult = () => AppError.Rule(ErrorCodes.LastDoneColumn, "Done is the board's only \"done\" column, and every board needs one.");
        var cut = RenderEditor();

        cut.Find("#column-3-type").Change("InProgress");

        Assert.Contains("only \"done\" column", Row(cut, 2).QuerySelector("[data-testid=column-error]")!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US3_AS4_A_limit_is_set_and_removed()
    {
        var cut = RenderEditor();

        cut.Find("#column-1-limit").Change("4");
        cut.Find("#column-2-limit").Change("");

        Assert.Equal(["limit 1 4 v7", "limit 2 none v8"], _columns.Calls);
    }

    [Fact]
    public void US3_AS5_Deleting_a_column_with_work_items_asks_where_they_go()
    {
        var cut = RenderEditor();

        Button(Row(cut, 1), "Delete").Click();
        var dialog = cut.WaitForElement("[data-testid=delete-column]");
        Assert.Equal(["", "1", "3"], dialog.QuerySelectorAll("#delete-destination option").Select(o => o.GetAttribute("value") ?? ""));
        dialog.QuerySelector("button.btn-danger")!.Click();
        Assert.Contains("Choose the column", cut.Find("#delete-destination-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(_columns.Calls);

        cut.Find("#delete-destination").Change("3");
        cut.Find("[data-testid=delete-column] button.btn-danger").Click();

        Assert.Equal(["delete 2 to 3 v7"], _columns.Calls);
        Assert.Equal(["To Do", "Done"], cut.FindAll(".column-name").Select(n => n.TextContent));
    }

    [Fact]
    public void An_empty_column_is_deleted_without_a_destination()
    {
        _columns.Columns.Insert(2, new BoardColumnView(4, "Blocked", StatusCategory.InProgress, 2, null, 0, true));
        var cut = RenderEditor();

        Button(Row(cut, 2), "Delete").Click();
        Assert.Empty(cut.FindAll("#delete-destination"));
        cut.Find("[data-testid=delete-column] button.btn-danger").Click();

        Assert.Equal(["delete 4 to none v7"], _columns.Calls);
    }

    [Fact]
    public void A_conflict_shows_the_latest_columns()
    {
        var latest = _columns.View() with { BoardVersion = 9, Columns = [.. _columns.View().Columns, new BoardColumnView(5, "QA", StatusCategory.InProgress, 3, null, 0, true)] };
        _columns.NextResult = () => AppError.Conflict("The columns were changed by someone else. The latest columns are shown; check them and try again.", latest);
        var cut = RenderEditor();

        Button(Row(cut, 0), "Move right").Click();

        Assert.Contains("changed by someone else", cut.Find("[data-testid=columns-conflict]").TextContent, StringComparison.Ordinal);
        Assert.Equal(["To Do", "In Progress", "Done", "QA"], cut.FindAll(".column-name").Select(n => n.TextContent));
        Button(Row(cut, 0), "Move right").Click();
        Assert.Equal("move 1 1 v9", _columns.Calls[^1]); // the next change is based on the latest version
    }

    [Fact]
    public void Adding_is_disabled_when_the_board_has_ten_columns()
    {
        for (var i = 0; i < 7; i++)
        {
            _columns.Columns.Insert(2, new BoardColumnView(20 + i, $"Step {i}", StatusCategory.InProgress, 0, null, 0, true));
        }

        var cut = RenderEditor();

        Assert.True(cut.Find("[data-testid=add-column]").HasAttribute("disabled"));
        Assert.Contains("the most it can have", cut.Find("form.add-column").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US3_AS8_The_settings_link_is_hidden_from_users_who_cannot_manage_the_project()
    {
        var boards = new FakeBoardService();
        boards.Board = boards.Board with { CanManageColumns = false };
        Services.AddSingleton<IBoardService>(boards);

        var cut = Render<BoardPage>(p => p.Add(x => x.Key, "WEB"));

        Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Contains("Project settings", StringComparison.Ordinal));
    }
}
