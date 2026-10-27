using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Layout;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Board;

/// <summary>The board page (FR-017 to FR-022).</summary>
public sealed class BoardPageTests : BunitTestBase
{
    private readonly FakeBoardService _board = new();
    private readonly FakeSprintService _sprints = new();

    public BoardPageTests()
    {
        Services.AddSingleton<IBoardService>(_board);
        Services.AddSingleton<ISprintService>(_sprints);
        Services.AddScoped<LiveAnnouncer>();
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<ViewerToday>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
    }

    private IRenderedComponent<BoardPage> RenderBoard() => Render<BoardPage>(p => p.Add(x => x.Key, "WEB"));

    private static readonly ActiveSprintInfo ActiveSprint = new(11, 1, "WEB Sprint 1", "Ship the home page", new DateOnly(2026, 9, 21),
        new DateOnly(2026, 10, 5), [4, 2], [new SprintOption(12, "WEB Sprint 2")]);

    [Fact]
    public void P3_US2_AS5_A_Scrum_board_shows_its_sprint_and_Project_Admins_complete_it()
    {
        _board.Board = _board.Board with { Template = Upms.Domain.Common.ProjectTemplate.Scrum, Sprint = ActiveSprint, CanManageSprints = true };

        var cut = RenderBoard();

        var banner = cut.Find("[data-testid=sprint-banner]");
        Assert.Equal("WEB Sprint 1", banner.QuerySelector("h2")!.TextContent.Trim());
        Assert.Contains("21 Sep – 5 Oct 2026", banner.TextContent, StringComparison.Ordinal);
        Assert.Equal("Ship the home page", cut.Find("[data-testid=sprint-goal]").TextContent.Trim());
        Assert.Empty(cut.FindAll("a[href*='done=']"));

        cut.Find("[data-testid=complete-sprint]").Click();
        Assert.Contains("0 items are done and 3 are open.", cut.Find("[data-testid=complete-sprint-dialog]").TextContent, StringComparison.Ordinal);
        cut.FindAll("[data-testid=complete-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Complete sprint").Click();

        Assert.Equal((11L, (long?)null), Assert.Single(_sprints.Completions));
    }

    [Fact]
    public void P3_US2_AS10_Members_of_a_Scrum_project_see_the_sprint_but_cannot_complete_it()
    {
        _board.Board = _board.Board with { Template = Upms.Domain.Common.ProjectTemplate.Scrum, Sprint = ActiveSprint, CanManageSprints = false };

        var cut = RenderBoard();

        Assert.NotEmpty(cut.FindAll("[data-testid=sprint-banner]"));
        Assert.Empty(cut.FindAll("[data-testid=complete-sprint]"));
    }

    [Fact]
    public void P3_US2_AS9_Without_an_active_sprint_a_Scrum_board_says_so_and_links_to_the_backlog()
    {
        _board.Board = _board.Board with
        {
            Template = Upms.Domain.Common.ProjectTemplate.Scrum,
            Columns = _board.Board.Columns.Select(c => c with { Cards = [], CardCount = 0 }).ToList(),
        };

        var cut = RenderBoard();

        Assert.Contains("No active sprint", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("spaces/WEB/backlog", cut.Find("[data-testid=plan-sprint]").GetAttribute("href"));
        // Space admins still see the columns, empty, to change them there (Jira parity pass FR-013); nobody adds cards there.
        Assert.Equal(3, cut.FindAll("[data-testid=column]").Count);
        Assert.Empty(cut.FindAll("[data-testid=column] article.bcard"));
        Assert.Empty(cut.FindAll("[data-testid=column-drop-end]"));

        _board.Board = _board.Board with { CanManageColumns = false };
        Assert.Empty(RenderBoard().FindAll("[data-testid=column]"));
    }

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
        Assert.Equal("spaces/WEB/board?task=WEB-1", cut.Find("#card-WEB-1-title").GetAttribute("href"));
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

    /// <summary>The Create form's requests: a column's "+ Create" opens it in that column.</summary>
    private List<(string? Space, long? Status)> CreateRequests()
    {
        var requests = new List<(string? Space, long? Status)>();
        Services.GetRequiredService<CreateWorkLauncher>().Requested += (space, status, _) => requests.Add((space, status));
        return requests;
    }

    [Fact]
    public void US1_AS5_A_column_s_Create_opens_the_Create_form_in_that_column_and_the_new_item_shows()
    {
        var requests = CreateRequests();
        var cut = RenderBoard();

        cut.FindAll("[data-testid=column]")[1].QuerySelector(".col-add")!.Click();

        Assert.Equal([("WEB", (long?)FakeBoardService.InProgress)], requests);
        var loads = _board.Loads;
        cut.InvokeAsync(() => Services.GetRequiredService<CreateWorkLauncher>().NotifyCreated("WEB", "WEB-9"));
        cut.WaitForAssertion(() => Assert.True(_board.Loads > loads, "the board reloads after an item is created in its space"));
    }

    [Fact]
    public void An_unknown_project_shows_not_found()
    {
        var cut = Render<BoardPage>(p => p.Add(x => x.Key, "NOPE"));

        Assert.Contains("Not found", cut.Find("h1").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P3_FR009_Cards_show_their_type_as_a_symbol_with_its_name_and_their_estimate()
    {
        _board.Board = _board.Board with
        {
            Columns =
            [
                _board.Board.Columns[0] with
                {
                    Cards =
                    [
                        FakeBoardService.Card("WEB-1", "Refund to card", Priority.Medium, FakeBoardService.ToDo, type: WorkItemType.Story, estimate: 2.5m),
                        FakeBoardService.Card("WEB-2", "Refund shows twice", Priority.Medium, FakeBoardService.ToDo, type: WorkItemType.Bug),
                    ],
                },
                .. _board.Board.Columns.Skip(1),
            ],
        };

        var cut = RenderBoard();

        var story = cut.Find("[data-testid='card-WEB-1']");
        Assert.Equal("Story", story.QuerySelector(".type-icon .sr-only")!.TextContent.Trim());
        Assert.Contains("2.5", story.QuerySelector("[data-testid=card-estimate]")!.TextContent, StringComparison.Ordinal);
        var bug = cut.Find("[data-testid='card-WEB-2']");
        Assert.Equal("Bug", bug.QuerySelector(".type-icon .sr-only")!.TextContent.Trim());
        Assert.Null(bug.QuerySelector("[data-testid=card-estimate]"));
    }

    [Theory]
    [InlineData(Upms.Domain.Common.ProjectTemplate.Kanban)]
    [InlineData(Upms.Domain.Common.ProjectTemplate.Scrum)]
    [InlineData(Upms.Domain.Common.ProjectTemplate.StageGate)]
    public void P3_FR010_Every_column_creates_through_the_Create_form_where_the_type_is_chosen(Upms.Domain.Common.ProjectTemplate template)
    {
        // A Scrum board shows its columns while a sprint is active (Phase 3 FR-027). The form offers the space's types.
        _board.Board = _board.Board with { Template = template, Sprint = template == Upms.Domain.Common.ProjectTemplate.Scrum ? ActiveSprint : null };
        var requests = CreateRequests();

        var cut = RenderBoard();

        Assert.Empty(cut.FindAll("select.type-choice"));
        Assert.Equal(["Create a work item in To Do", "Create a work item in In Progress", "Create a work item in Done"],
            cut.FindAll("[data-testid=column] .col-add").Select(b => b.GetAttribute("aria-label")));
        cut.FindAll("[data-testid=column]")[0].QuerySelector(".col-add")!.Click();
        Assert.Equal([("WEB", (long?)FakeBoardService.ToDo)], requests);
    }

    [Fact]
    public void P3_FR006_The_header_names_the_template()
    {
        _board.Board = _board.Board with { Template = Upms.Domain.Common.ProjectTemplate.Scrum };

        var cut = RenderBoard();

        Assert.Equal(", Scrum space", cut.Find(".space-title-row h1 + .sr-only").TextContent);
    }
}
