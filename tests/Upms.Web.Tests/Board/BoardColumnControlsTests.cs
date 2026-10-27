using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Board;

/// <summary>Columns changed on the board itself (Jira parity pass US2, FR-010–FR-013; research R12). The fake board has To Do (2
/// items), In Progress (1 item, limit 3) and an empty Done.</summary>
public sealed class BoardColumnControlsTests : BunitTestBase
{
    private readonly FakeBoardService _board = new();
    private readonly FakeBoardColumnService _columns = new();

    public BoardColumnControlsTests()
    {
        Services.AddSingleton<IBoardService>(_board);
        Services.AddSingleton<IBoardColumnService>(_columns);
        Services.AddSingleton<ISprintService>(new FakeSprintService());
        Services.AddScoped<LiveAnnouncer>();
    }

    private IRenderedComponent<BoardPage> RenderBoard() => Render<BoardPage>(p => p.Add(x => x.Key, "WEB"));

    private static void OpenMenu(IRenderedComponent<BoardPage> cut, long columnId) => cut.Find($"[data-testid=column-menu-{columnId}]").Click();

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<BoardPage> cut, string text) =>
        cut.FindAll("[role=menu] [role=menuitem]").Single(i => i.TextContent.Trim() == text);

    [Fact]
    public void JP_US2_AS1_Plus_after_the_last_column_adds_one_with_its_status_type()
    {
        var cut = RenderBoard();
        var loads = _board.Loads;

        cut.Find("[data-testid=add-column-button]").Click();
        cut.Find("#board-new-column-name").Input("Review");
        cut.Find("#board-new-column-type").Change("InProgress");
        cut.Find("[data-testid=board-add-column] .btn-primary").Click();

        Assert.Equal(["add Review InProgress 3 v7"], _columns.Calls);
        Assert.True(_board.Loads > loads, "The board shows the new column.");
        Assert.Equal("Add column", cut.Find("[data-testid=add-column-button]").GetAttribute("aria-label"));
    }

    [Fact]
    public void JP_US2_AS2_The_column_menu_renames_limits_and_moves()
    {
        var cut = RenderBoard();

        var menuButton = cut.Find("[data-testid=column-menu-2]");
        Assert.Equal(("Column actions for In Progress", "menu", "false"),
            (menuButton.GetAttribute("aria-label"), menuButton.GetAttribute("aria-haspopup"), menuButton.GetAttribute("aria-expanded")));

        OpenMenu(cut, 2);
        Assert.Equal("true", cut.Find("[data-testid=column-menu-2]").GetAttribute("aria-expanded"));
        Assert.Equal(["Rename", "Change limit", "Clear limit", "Move left", "Move right", "Change status type", "Delete"],
            cut.FindAll("[role=menu] [role=menuitem]").Select(i => i.TextContent.Trim()));

        Item(cut, "Rename").Click();
        cut.Find("#board-rename-input").Input("Building");
        cut.Find("[data-testid=board-rename-column] .btn-primary").Click();

        OpenMenu(cut, 2);
        Item(cut, "Change limit").Click();
        cut.Find("#board-limit-input").Change("4");
        cut.Find("[data-testid=board-column-limit] .btn-primary").Click();

        OpenMenu(cut, 2);
        Item(cut, "Clear limit").Click();
        OpenMenu(cut, 2);
        Item(cut, "Move left").Click();

        Assert.Equal(["rename 2 Building v7", "limit 2 4 v8", "limit 2 none v9", "move 2 0 v10"], _columns.Calls);
    }

    [Fact]
    public void The_first_and_last_columns_cannot_move_past_the_edge_and_a_column_without_a_limit_offers_set_limit()
    {
        var cut = RenderBoard();

        OpenMenu(cut, 1);

        Assert.Equal("true", Item(cut, "Move left").GetAttribute("aria-disabled"));
        Assert.Contains(cut.FindAll("[role=menuitem]"), i => i.TextContent.Trim() == "Set limit");
        Assert.DoesNotContain(cut.FindAll("[role=menuitem]"), i => i.TextContent.Trim() == "Clear limit");
    }

    [Fact]
    public void JP_US2_AS3_Only_empty_columns_can_change_their_status_type()
    {
        var cut = RenderBoard();

        OpenMenu(cut, 1);
        var locked = Item(cut, "Change status type");
        Assert.Equal("true", locked.GetAttribute("aria-disabled"));
        Assert.Contains("Only empty columns can change type.", cut.Find($"#{locked.GetAttribute("aria-describedby")}").TextContent, StringComparison.Ordinal);
        locked.Click();
        Assert.Empty(cut.FindAll("[data-testid=board-column-type] select"));

        cut.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        OpenMenu(cut, 3);
        Item(cut, "Change status type").Click();
        cut.Find("#board-column-type-select").Change("InProgress");
        cut.Find("[data-testid=board-column-type] .btn-primary").Click();

        Assert.Equal(["type 3 InProgress v7"], _columns.Calls);
    }

    [Fact]
    public void JP_US2_AS4_Deleting_a_column_with_cards_asks_where_they_go()
    {
        var cut = RenderBoard();

        OpenMenu(cut, 2);
        Item(cut, "Delete").Click();
        var dialog = cut.Find("[data-testid=delete-column]");
        Assert.Contains("Move its work items (1) to", dialog.TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=delete-column] .btn-danger").Click();
        Assert.Equal("Choose the column its work items should move to.", cut.Find("#delete-destination-error").TextContent);

        cut.Find("#delete-destination").Change("1");
        cut.Find("[data-testid=delete-column] .btn-danger").Click();

        Assert.Equal(["delete 2 to 1 v7"], _columns.Calls);
    }

    [Fact]
    public void JP_US2_AS4_The_only_to_do_or_done_column_cannot_be_deleted()
    {
        var cut = RenderBoard();

        OpenMenu(cut, 3);
        Item(cut, "Delete").Click();

        Assert.Contains("Done is the board's only \"done\" column", cut.Find("[data-testid=delete-blocked]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=delete-column] .btn-danger"));
    }

    [Fact]
    public void JP_US2_AS5_Members_and_viewers_see_no_column_controls()
    {
        _board.Board = _board.Board with { CanManageColumns = false };

        var cut = RenderBoard();

        Assert.Empty(cut.FindAll("[data-testid=add-column-button]"));
        Assert.Empty(cut.FindAll("[data-testid^=column-menu-]"));
        Assert.Empty(_columns.Calls);
    }

    [Fact]
    public void JP_US2_AS6_A_refusal_is_shown_where_it_was_made_and_the_text_stays()
    {
        _columns.NextResult = () => AppError.Rule("DuplicateColumnName", "There is already a column called Done.");
        var cut = RenderBoard();

        OpenMenu(cut, 2);
        Item(cut, "Rename").Click();
        cut.Find("#board-rename-input").Input("Done");
        cut.Find("[data-testid=board-rename-column] .btn-primary").Click();

        Assert.Equal("There is already a column called Done.", cut.Find("#board-rename-error").TextContent);
        Assert.Equal("Done", cut.Find("#board-rename-input").GetAttribute("value"));
    }

    [Fact]
    public void JP_US2_AS6_A_change_made_meanwhile_shows_the_current_columns()
    {
        var cut = RenderBoard();
        var loads = _board.Loads;
        _columns.NextResult = () => AppError.Conflict("The columns were changed by someone else. The board now shows the current columns.", _columns.View());

        OpenMenu(cut, 2);
        Item(cut, "Move right").Click();

        Assert.Contains("changed by someone else", cut.Find("[data-testid=columns-conflict]").TextContent, StringComparison.Ordinal);
        Assert.True(_board.Loads > loads);
    }

    [Fact]
    public void The_menu_works_with_the_keyboard_and_gives_focus_back_to_its_button()
    {
        var cut = RenderBoard();

        OpenMenu(cut, 2);
        Assert.StartsWith("column-2-action-", JSInterop.VerifyInvoke("upms.focusById").Arguments[0]!.ToString(), StringComparison.Ordinal);
        cut.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[role=menu]"));
        Assert.Equal("column-menu-button-2", JSInterop.Invocations["upms.focusById"][^1].Arguments[0]);
    }
}
