using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;
using static Upms.Web.Tests.Fakes.FakeBoardService;

namespace Upms.Web.Tests.Board;

/// <summary>The board's quick filters (Phase 4 FR-047, research R19; contracts/ui-routes.md): label, type and epic (stage
/// on Stage-gate boards) as toggle buttons that combine with "Only my tasks" and the assignee filter, count the matches
/// per column, and live in the address.</summary>
public sealed class BoardQuickFilterTests : BunitTestBase
{
    private static readonly AssigneeRef Me = AssigneeRef.Of(Viewer, "Amina Khan", canWork: true);
    private static readonly ContainerView Refunds = new("MOB-1", "Card refunds", WorkItemType.Epic);
    private static readonly ContainerView Onboarding = new("MOB-2", "Onboarding", WorkItemType.Epic);
    private readonly FakeBoardService _boards = new();

    public BoardQuickFilterTests()
    {
        Services.AddSingleton<IBoardService>(_boards);
        Services.AddScoped<LiveAnnouncer>();
        _boards.Board = _boards.Board with
        {
            ProjectKey = "MOB",
            Template = ProjectTemplate.Kanban,
            Columns =
            [
                new ColumnView(ToDo, "To Do", StatusCategory.ToDo, null, 3, false,
                [
                    Card("MOB-3", "Refund fails", Priority.High, ToDo, Me, type: WorkItemType.Bug) with { Labels = ["launch"], Container = Refunds },
                    Card("MOB-4", "Refund history", Priority.Medium, ToDo, type: WorkItemType.Story) with { Container = Refunds },
                    Card("MOB-5", "Sign-up crash", Priority.High, ToDo, type: WorkItemType.Bug) with { Labels = ["Payments"], Container = Onboarding },
                ]),
                new ColumnView(InProgress, "In Progress", StatusCategory.InProgress, null, 1, false,
                [
                    Card("MOB-6", "Refund e-mail", Priority.Low, InProgress, type: WorkItemType.Bug) with { Container = Refunds },
                ]),
                new ColumnView(Done, "Done", StatusCategory.Done, null, 0, false, []),
            ],
        };
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private string Query => new Uri(Navigation.Uri).Query;

    /// <summary>The board with the toolbar's "Filter" panel open, where the quick filters are, as in Jira.</summary>
    private IRenderedComponent<BoardPage> Open(string query = "")
    {
        Navigation.NavigateTo($"spaces/MOB/board{query}");
        return OpenFilter(Render<BoardPage>(p => p.Add(x => x.Key, "MOB")));
    }

    private static IRenderedComponent<BoardPage> OpenFilter(IRenderedComponent<BoardPage> cut)
    {
        cut.Find("button[aria-controls=board-filter-panel]").Click();
        return cut;
    }

    private static List<string> Keys(IRenderedComponent<BoardPage> cut) =>
        cut.FindAll("article.bcard").Select(c => c.GetAttribute("data-drag-key")!).ToList();

    private static List<string> Counts(IRenderedComponent<BoardPage> cut) =>
        cut.FindAll("[data-testid=column] .count").Select(c => c.TextContent.Trim()).ToList();

    [Fact]
    public void P4_US4_AS7_Quick_filters_offer_the_types_epics_and_labels_on_the_board()
    {
        var cut = Open();

        Assert.Equal(["Story", "Task", "Bug"], cut.FindAll("[role=group][aria-label=Types] button").Select(b => b.TextContent));
        Assert.Equal(["Card refunds", "Onboarding"], cut.FindAll("[role=group][aria-label=Epics] button").Select(b => b.TextContent));
        Assert.Equal(["launch", "Payments"], cut.FindAll("[role=group][aria-label=Labels] button").Select(b => b.TextContent));
        Assert.All(cut.FindAll(".quick-filters button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
        Assert.Equal(["launch"], cut.Find("[data-testid='card-MOB-3'] [data-testid=card-labels]").Children.Select(l => l.TextContent));
    }

    [Fact]
    public void P4_US4_AS7_Bug_and_the_epic_leave_matching_cards_with_column_counts_kept_in_the_address()
    {
        var cut = Open();

        cut.Find("[data-testid=quick-kind-bug]").Click();
        cut.Find("[data-testid='quick-container-MOB-1']").Click();

        Assert.Equal("?kinds=bug&containers=MOB-1", Query);
        Assert.Equal(["MOB-3", "MOB-6"], Keys(cut));
        Assert.Equal(["1 of 3", "1 of 1", "0 of 0"], Counts(cut));
        Assert.Equal("true", cut.Find("[data-testid=quick-kind-bug]").GetAttribute("aria-pressed"));
        Assert.Equal("true", cut.Find("[data-testid='quick-container-MOB-1']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void P4_US4_AS7_Quick_filters_combine_with_only_my_tasks_and_the_assignee_filter()
    {
        var cut = Open("?kinds=bug");
        Assert.Equal(["MOB-3", "MOB-5", "MOB-6"], Keys(cut));

        cut.Find("[data-testid=only-mine]").Click();
        Assert.Equal("?kinds=bug&assignee=me", Query);
        Assert.Equal(["MOB-3"], Keys(cut));

        cut.Find("#assignee-filter").Change("unassigned");
        Assert.Equal(["MOB-5", "MOB-6"], Keys(cut));
        Assert.Equal("false", cut.Find("[data-testid=only-mine]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Labels_match_any_chosen_one_regardless_of_case_and_clear_filters_shows_everything()
    {
        var cut = Open("?labels=LAUNCH,payments");

        Assert.Equal(["MOB-3", "MOB-5"], Keys(cut));
        Assert.Equal("true", cut.Find("[data-testid=quick-label-launch]").GetAttribute("aria-pressed"));

        cut.Find("[data-testid=clear-board-filters]").Click();

        Assert.Equal("", Query);
        Assert.Equal(4, Keys(cut).Count);
        Assert.Empty(cut.FindAll("[data-testid=clear-board-filters]"));
    }

    [Fact]
    public void A_toggle_turns_off_again()
    {
        var cut = Open("?kinds=bug,story");

        cut.Find("[data-testid=quick-kind-story]").Click();

        Assert.Equal("?kinds=bug", Query);
    }

    [Fact]
    public void A_new_filter_builds_on_the_last_one_asked_for()
    {
        var slow = new SlowNavigationManager("spaces/MOB/board");
        Services.AddSingleton<NavigationManager>(slow);
        var cut = OpenFilter(Render<BoardPage>(p => p.Add(x => x.Key, "MOB")));
        slow.Holding = true;

        cut.Find("[data-testid=quick-kind-bug]").Click();
        cut.Find("[data-testid=quick-label-launch]").Click();
        cut.InvokeAsync(slow.Arrive);

        Assert.Equal("?labels=launch&kinds=bug", new Uri(slow.Uri).Query);
    }

    [Fact]
    public void Opening_a_card_keeps_the_filters()
    {
        var cut = Open("?kinds=bug");

        Assert.Equal("spaces/MOB/board?kinds=bug&task=MOB-3", cut.Find("#card-MOB-3-title").GetAttribute("href"));
    }

    [Fact]
    public void Stage_gate_boards_offer_stages_in_place_of_epics()
    {
        _boards.Board = _boards.Board with { Template = ProjectTemplate.StageGate };

        var cut = Open();

        Assert.Single(cut.FindAll("[role=group][aria-label=Stages]"));
        Assert.Empty(cut.FindAll("[role=group][aria-label=Epics]"));
        Assert.Empty(cut.FindAll("[role=group][aria-label=Types]")); // Stage-gate boards hold tasks only
    }
}
