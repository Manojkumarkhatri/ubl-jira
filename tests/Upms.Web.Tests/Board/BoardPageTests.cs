using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Board;

/// <summary>The board page (FR-017 to FR-022).</summary>
public sealed class BoardPageTests : BunitTestBase
{
    private readonly FakeBoardService _board = new();

    public BoardPageTests()
    {
        Services.AddSingleton<IBoardService>(_board);
        Services.AddScoped<LiveAnnouncer>();
    }

    private IRenderedComponent<BoardPage> RenderBoard() => Render<BoardPage>(p => p.Add(x => x.Key, "WEB"));

    [Fact]
    public void Columns_show_their_name_and_card_count_and_cards_show_key_title_and_priority()
    {
        var cut = RenderBoard();

        var columns = cut.FindAll("[data-testid=column]");
        Assert.Equal(3, columns.Count);
        Assert.Contains("To Do", columns[0].QuerySelector("h2")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("2", columns[0].QuerySelector(".count")!.TextContent.Trim());

        var card = cut.Find("[data-testid='card-WEB-1']");
        Assert.Contains("WEB-1", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("Design the home page", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("High priority", card.TextContent, StringComparison.Ordinal);
        Assert.Equal("true", card.GetAttribute("draggable"));
    }

    [Fact]
    public void P2_US1_AS3_Viewers_get_a_read_only_board()
    {
        _board.Board = _board.Board with { CanContribute = false };

        var cut = RenderBoard();

        Assert.Empty(cut.FindAll("input.inline-input"));
        Assert.Empty(cut.FindAll("[data-testid=column-drop-end]"));
        Assert.Empty(cut.FindAll(".move-btn"));
        Assert.All(cut.FindAll("article.bcard"), card => Assert.Equal("false", card.GetAttribute("draggable")));
        Assert.Equal("projects/WEB/board?task=WEB-1", cut.Find("#card-WEB-1-title").GetAttribute("href"));
    }

    [Fact]
    public void US1_AS7_Dropping_a_card_on_another_moves_it_before_that_card()
    {
        var cut = RenderBoard();

        cut.Find("[data-testid='card-WEB-2']").DragStart();
        cut.Find("[data-testid='card-WEB-1']").Drop();

        var move = Assert.Single(_board.Moves);
        Assert.Equal(("WEB-2", FakeBoardService.ToDo), (move.Key, move.ColumnId));
        Assert.Equal(new CardPlacement.Before("WEB-1"), move.Placement);
    }

    [Fact]
    public void US1_AS6_Dropping_on_a_column_footer_moves_the_card_to_its_end()
    {
        var cut = RenderBoard();

        cut.Find("[data-testid='card-WEB-1']").DragStart();
        cut.FindAll("[data-testid=column-drop-end]")[1].Drop();

        var move = Assert.Single(_board.Moves);
        Assert.Equal(("WEB-1", FakeBoardService.InProgress, CardPlacement.AtEnd), (move.Key, move.ColumnId, move.Placement));
    }

    [Fact]
    public void US1_AS8_Move_to_issues_the_same_call_as_a_drop()
    {
        var cut = RenderBoard();

        var card = cut.Find("[data-testid='card-WEB-1']");
        card.QuerySelector(".move-btn")!.Click();
        cut.FindAll("[data-testid='card-WEB-1'] .move-list button").Single(b => b.TextContent.Trim() == "Done: bottom").Click();

        var move = Assert.Single(_board.Moves);
        Assert.Equal(("WEB-1", FakeBoardService.Done, CardPlacement.AtEnd), (move.Key, move.ColumnId, move.Placement));
        Assert.Equal(new byte[] { 1, 2, 3 }, move.Version);
    }

    [Fact]
    public void US1_AS9_A_conflicting_move_shows_the_conflict_and_reloads_the_board()
    {
        _board.NextMoveResult = () => AppError.Conflict("WEB-1 was changed by someone else.",
            FakeBoardService.Card("WEB-1", "Design the home page", Priority.High, FakeBoardService.InProgress));
        var cut = RenderBoard();
        var loadsBefore = _board.Loads;

        cut.Find("[data-testid='card-WEB-1']").DragStart();
        cut.FindAll("[data-testid=column-drop-end]")[2].Drop();

        cut.WaitForAssertion(() =>
            Assert.Contains("changed by someone else", cut.Find("[data-testid=conflict]").TextContent, StringComparison.Ordinal));
        Assert.True(_board.Loads > loadsBefore);
    }

    [Fact]
    public void US1_AS5_Typing_in_a_column_creates_a_task_there()
    {
        var cut = RenderBoard();

        var input = cut.FindAll("[data-testid=column]")[1].QuerySelector("input")!;
        input.Input("Build the header");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([(FakeBoardService.InProgress, "Build the header")], _board.Created);
        Assert.True(_board.Loads >= 2, "the board reloads after a task is created");
    }

    [Fact]
    public void An_unknown_project_shows_not_found()
    {
        var cut = Render<BoardPage>(p => p.Add(x => x.Key, "NOPE"));

        Assert.Contains("Not found", cut.Find("h1").TextContent, StringComparison.Ordinal);
    }
}
