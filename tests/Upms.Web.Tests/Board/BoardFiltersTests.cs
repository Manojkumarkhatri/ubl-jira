using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;
using static Upms.Web.Tests.Fakes.FakeBoardService;

namespace Upms.Web.Tests.Board;

/// <summary>Assignees and due dates on cards, and the board's filters (Phase 2 FR-020–FR-022, FR-024). The viewer is
/// Amina Khan; "today" is 27 Sep 2026.</summary>
public sealed class BoardFiltersTests : BunitTestBase
{
    private static readonly Guid Bilal = Guid.NewGuid();
    private static readonly AssigneeRef Me = AssigneeRef.Of(Viewer, "Amina Khan", canWork: true);
    private static readonly AssigneeRef BilalRef = AssigneeRef.Of(Bilal, "Bilal Ahmed", canWork: true);
    private readonly FakeBoardService _boards = new();

    public BoardFiltersTests()
    {
        Services.AddSingleton<IBoardService>(_boards);
        Services.AddScoped<LiveAnnouncer>();
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<ViewerToday>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
        _boards.Board = _boards.Board with
        {
            Columns =
            [
                new ColumnView(ToDo, "To Do", StatusCategory.ToDo, 3, 5, true,
                [
                    Card("WEB-1", "Design the home page", Priority.High, ToDo, Me, new DateOnly(2026, 9, 26)),
                    Card("WEB-2", "Write the copy", Priority.Medium, ToDo, BilalRef, new DateOnly(2026, 10, 4)),
                    Card("WEB-3", "Pick the colours", Priority.Low, ToDo, Me),
                    Card("WEB-4", "Book the photographer", Priority.Low, ToDo),
                    Card("WEB-5", "Order the logo", Priority.Low, ToDo, AssigneeRef.Of(Guid.NewGuid(), "Gone Away", canWork: false)),
                ]),
                new ColumnView(InProgress, "In Progress", StatusCategory.InProgress, null, 0, false, []),
                new ColumnView(Done, "Done", StatusCategory.Done, null, 1, false,
                [
                    Card("WEB-6", "Set up hosting", Priority.Medium, Done, Me, new DateOnly(2026, 9, 20)),
                ]),
            ],
        };
    }

    private IRenderedComponent<BoardPage> RenderBoard() => Render<BoardPage>(p => p.Add(x => x.Key, "WEB"));

    private static List<string> Keys(IRenderedComponent<BoardPage> cut) =>
        cut.FindAll("article.bcard").Select(c => c.GetAttribute("data-drag-key")!).ToList();

    [Fact]
    public void Cards_show_the_assignees_initials_with_the_name_as_text_and_the_due_date()
    {
        var cut = RenderBoard();

        var card = cut.Find("[data-testid='card-WEB-2']");
        Assert.Equal("BA", card.QuerySelector(".avatar")!.TextContent.Trim());
        Assert.Equal("true", card.QuerySelector(".avatar")!.GetAttribute("aria-hidden"));
        Assert.Contains("Assigned to Bilal Ahmed", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("4 Oct", card.QuerySelector("[data-testid=due-date]")!.TextContent, StringComparison.Ordinal);
        Assert.Empty(card.QuerySelectorAll(".overdue-label"));
        Assert.Empty(cut.Find("[data-testid='card-WEB-4']").QuerySelectorAll(".avatar"));
    }

    [Fact]
    public void P2_US2_AS5_Open_cards_due_before_today_are_marked_Overdue_and_done_ones_are_not()
    {
        var cut = RenderBoard();

        Assert.Equal("Overdue", cut.Find("[data-testid='card-WEB-1'] .overdue-label").TextContent.Trim());
        Assert.Empty(cut.Find("[data-testid='card-WEB-6']").QuerySelectorAll(".overdue-label"));
    }

    [Fact]
    public void P2_US2_AS8_An_assignee_no_longer_on_the_project_is_marked_in_words()
    {
        var cut = RenderBoard();

        Assert.Contains("no longer on the project", cut.Find("[data-testid='card-WEB-5']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("no longer on the project", cut.Find("[data-testid='card-WEB-2']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P2_US2_AS6_Only_my_tasks_hides_other_cards_and_columns_count_the_matches()
    {
        var cut = RenderBoard();

        cut.Find("[data-testid=only-mine]").Click();

        Assert.Equal(["WEB-1", "WEB-3", "WEB-6"], Keys(cut));
        Assert.Equal("true", cut.Find("[data-testid=only-mine]").GetAttribute("aria-pressed"));
        var columns = cut.FindAll("[data-testid=column]");
        Assert.Equal("2 of 5", columns[0].QuerySelector(".count")!.TextContent.Trim());
        Assert.Contains("limit 3", columns[0].QuerySelector("header")!.TextContent, StringComparison.Ordinal);
        Assert.NotNull(columns[0].QuerySelector(".over-badge")); // the limit still counts all five cards
        Assert.Equal("0 of 0", columns[1].QuerySelector(".count")!.TextContent.Trim());

        cut.Find("[data-testid=only-mine]").Click();

        Assert.Equal(6, Keys(cut).Count);
        Assert.Equal("5 of 3", cut.FindAll("[data-testid=column]")[0].QuerySelector(".count")!.TextContent.Trim());
    }

    [Fact]
    public void P2_US2_AS6_The_assignee_filter_offers_Unassigned_and_everyone_on_the_cards()
    {
        var cut = RenderBoard();

        var options = cut.FindAll("#assignee-filter option").Select(o => o.TextContent.Trim()).ToList();
        Assert.Equal(["Anyone", "Me (Amina Khan)", "Unassigned", "Bilal Ahmed", "Gone Away"], options);

        cut.Find("#assignee-filter").Change("unassigned");
        Assert.Equal(["WEB-4"], Keys(cut));

        cut.Find("#assignee-filter").Change(Bilal.ToString());
        Assert.Equal(["WEB-2"], Keys(cut));
        Assert.Equal("false", cut.Find("[data-testid=only-mine]").GetAttribute("aria-pressed"));

        cut.Find("#assignee-filter").Change("");
        Assert.Equal(6, Keys(cut).Count);
    }

    [Fact]
    public void Only_my_tasks_and_the_assignee_filter_are_the_same_choice()
    {
        var cut = RenderBoard();

        cut.Find("#assignee-filter").Change("me");

        Assert.Equal("true", cut.Find("[data-testid=only-mine]").GetAttribute("aria-pressed"));
        cut.Find("[data-testid=only-mine]").Click();
        Assert.Equal("", cut.Find("#assignee-filter").GetAttribute("value"));
    }

    [Fact]
    public void Dropping_next_to_a_visible_card_while_filtered_places_the_card_before_it()
    {
        var cut = RenderBoard();
        cut.Find("[data-testid=only-mine]").Click();

        cut.Find("[data-testid='card-WEB-3']").DragStart();
        cut.Find("[data-testid='card-WEB-1']").Drop();

        var move = Assert.Single(_boards.Moves);
        Assert.Equal(("WEB-3", ToDo, (CardPlacement)new CardPlacement.Before("WEB-1")), (move.Key, move.ColumnId, move.Placement));
    }
}
