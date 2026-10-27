using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;
using static Upms.Web.Tests.Fakes.FakeBoardService;

namespace Upms.Web.Tests.Board;

/// <summary>The board grouped into swimlanes by epic, assignee or parent, with lane moves (Jira parity pass US5 scenarios 3 to
/// 5, FR-026, FR-027; research R16, R17).</summary>
public sealed class SwimlaneBoardTests : BunitTestBase
{
    private static readonly AssigneeRef Sara = AssigneeRef.Of(Guid.NewGuid(), "Sara Khan", canWork: true);
    private static readonly ContainerView Refunds = new("MOB-1", "Card refunds", WorkItemType.Epic, EpicColor.Teal);
    private static readonly ContainerView Wallet = new("MOB-2", "Wallet", WorkItemType.Epic, EpicColor.Orange);
    private readonly FakeBoardService _boards = new();

    public SwimlaneBoardTests()
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
                    Card("MOB-3", "Refund to card", Priority.High, ToDo, Sara, type: WorkItemType.Story) with { Container = Refunds },
                    Card("MOB-7", "Ledger entry", Priority.Medium, ToDo, type: WorkItemType.Subtask) with { Container = Refunds, ParentKey = "MOB-3" },
                    Card("MOB-4", "Top up", Priority.Medium, ToDo, type: WorkItemType.Story) with { Container = Wallet },
                ]),
                new ColumnView(InProgress, "In Progress", StatusCategory.InProgress, null, 1, false,
                [
                    Card("MOB-5", "Sign-up crash", Priority.High, InProgress, type: WorkItemType.Bug),
                ]),
                new ColumnView(Done, "Done", StatusCategory.Done, null, 0, false, []),
            ],
        };
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private string Query => new Uri(Navigation.Uri).Query;

    private IRenderedComponent<BoardPage> Open(string query = "")
    {
        Navigation.NavigateTo($"spaces/MOB/board{query}");
        return Render<BoardPage>(p => p.Add(x => x.Key, "MOB"));
    }

    private static List<string> LaneTitles(IRenderedComponent<BoardPage> cut) =>
        cut.FindAll("[data-testid=lane] .lane-title").Select(t => t.TextContent.Trim()).ToList();

    private static AngleSharp.Dom.IElement Lane(IRenderedComponent<BoardPage> cut, string title) =>
        cut.FindAll("[data-testid=lane]").Single(l => l.QuerySelector(".lane-title")!.TextContent.Trim() == title);

    private static List<string> Keys(AngleSharp.Dom.IElement element) =>
        element.QuerySelectorAll("article.bcard").Select(c => c.GetAttribute("data-drag-key")!).ToList();

    /// <summary>The toolbar's "Group" button, and a choice of its menu.</summary>
    private static AngleSharp.Dom.IElement GroupButton(IRenderedComponent<BoardPage> cut) => cut.Find("button[aria-controls=board-group-panel]");

    private static void ChooseGroup(IRenderedComponent<BoardPage> cut, string group)
    {
        GroupButton(cut).Click();
        cut.Find($"[data-testid=group-{group}]").Click();
    }

    [Fact]
    public void JP_US5_AS3_Group_by_follows_the_address_and_lanes_show_their_cards_and_counts()
    {
        var cut = Open("?group=epic");

        Assert.Equal("Group: Epic", GroupButton(cut).TextContent.Trim());
        GroupButton(cut).Click();
        Assert.Equal("true", cut.Find("[data-testid=group-epic]").GetAttribute("aria-checked"));
        GroupButton(cut).Click();
        Assert.Equal(["MOB-1 Card refunds", "MOB-2 Wallet", "No epic"], LaneTitles(cut));
        Assert.Equal(["MOB-3", "MOB-7"], Keys(Lane(cut, "MOB-1 Card refunds")));
        Assert.Contains("2", Lane(cut, "MOB-1 Card refunds").QuerySelector(".lane-head .count")!.TextContent, StringComparison.Ordinal);

        ChooseGroup(cut, "assignee");

        Assert.Equal("?group=assignee", Query);
        Assert.Equal(["Sara Khan", "Unassigned"], LaneTitles(cut));
    }

    [Fact]
    public void Group_by_keeps_the_quick_filters_and_they_apply_before_grouping()
    {
        var cut = Open("?kinds=story");

        ChooseGroup(cut, "epic");

        Assert.Equal("?kinds=story&group=epic", Query);
        Assert.Equal(["MOB-1 Card refunds", "MOB-2 Wallet"], LaneTitles(cut));
    }

    [Fact]
    public void The_sub_task_type_filter_appears_when_the_board_has_sub_tasks()
    {
        var cut = Open();

        cut.Find("button[aria-controls=board-filter-panel]").Click();

        Assert.Equal(["Story", "Task", "Bug", "Sub-task"], cut.FindAll("[role=group][aria-label=Types] button").Select(b => b.TextContent));
    }

    [Fact]
    public void JP_US5_AS3_A_lane_header_collapses_and_expands_its_lane()
    {
        var cut = Open("?group=epic");
        var header = Lane(cut, "MOB-2 Wallet").QuerySelector(".lane-head button")!;
        Assert.Equal("true", header.GetAttribute("aria-expanded"));

        header.Click();

        var lane = Lane(cut, "MOB-2 Wallet");
        Assert.Equal("false", lane.QuerySelector(".lane-head button")!.GetAttribute("aria-expanded"));
        Assert.True(cut.Find($"#{lane.QuerySelector(".lane-head button")!.GetAttribute("aria-controls")}").HasAttribute("hidden"));
    }

    [Fact]
    public void JP_US5_AS4_A_move_within_its_own_lane_changes_only_the_column()
    {
        var cut = Open("?group=epic");

        cut.Find("[data-testid='card-MOB-4']").DragStart();
        Lane(cut, "MOB-2 Wallet").QuerySelectorAll("[data-testid=column-drop-end]")[1].Drop();

        Assert.Equal(("MOB-4", InProgress), (_boards.Moves.Single().Key, _boards.Moves.Single().ColumnId));
        Assert.Null(Assert.Single(_boards.Lanes));
    }

    [Fact]
    public void JP_US5_AS5_A_drop_in_another_lane_changes_the_epic_with_the_column()
    {
        var cut = Open("?group=epic");

        cut.Find("[data-testid='card-MOB-4']").DragStart();
        cut.FindAll("[data-testid=lane]")[0].QuerySelectorAll("[data-testid=column-drop-end]")[1].Drop();

        Assert.Equal(("MOB-4", InProgress), (_boards.Moves.Single().Key, _boards.Moves.Single().ColumnId));
        Assert.Equal(new LaneChange.Container("MOB-1"), Assert.Single(_boards.Lanes));
    }

    [Fact]
    public void JP_US5_AS5_Move_to_offers_the_other_lanes_and_sends_the_lane_change()
    {
        var cut = Open("?group=assignee");
        var card = cut.Find("[data-testid='card-MOB-4']");
        card.QuerySelector(".move-btn")!.Click();

        var lanes = cut.FindAll("[data-testid='card-MOB-4'] .move-list button.lane-option").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Lane: Sara Khan"], lanes);
        cut.FindAll("[data-testid='card-MOB-4'] .move-list button.lane-option")[0].Click();

        Assert.Equal(("MOB-4", ToDo), (_boards.Moves.Single().Key, _boards.Moves.Single().ColumnId));
        Assert.Equal(new LaneChange.Assignee(Sara.UserId), Assert.Single(_boards.Lanes));
    }

    [Fact]
    public void Sub_tasks_lanes_offer_no_lane_moves_and_a_sub_task_cannot_change_epic_lane()
    {
        var cut = Open("?group=subtasks");
        Assert.Equal(["MOB-3 Refund to card", "Everything else"], LaneTitles(cut));
        cut.Find("[data-testid='card-MOB-4'] .move-btn").Click();
        Assert.Empty(cut.FindAll("[data-testid='card-MOB-4'] .lane-option"));

        Navigation.NavigateTo("spaces/MOB/board?group=epic");
        cut.Find("[data-testid='card-MOB-7'] .move-btn").Click();
        Assert.Empty(cut.FindAll("[data-testid='card-MOB-7'] .lane-option"));
    }

    [Fact]
    public void A_drop_in_another_sub_tasks_lane_moves_only_the_column()
    {
        var cut = Open("?group=subtasks");

        cut.Find("[data-testid='card-MOB-7']").DragStart();
        Lane(cut, "Everything else").QuerySelectorAll("[data-testid=column-drop-end]")[1].Drop();

        Assert.Equal(("MOB-7", InProgress), (_boards.Moves.Single().Key, _boards.Moves.Single().ColumnId));
        Assert.Null(Assert.Single(_boards.Lanes));
    }

    [Fact]
    public void A_refused_lane_move_shows_why()
    {
        _boards.NextMoveResult = () => AppError.Rule(Upms.Application.Common.Results.ErrorCodes.NotAssignable,
            "That person can't be assigned: only active Space admins and Members of the space can.");
        var cut = Open("?group=assignee");

        cut.Find("[data-testid='card-MOB-4']").DragStart();
        Lane(cut, "Sara Khan").QuerySelectorAll("[data-testid=column-drop-end]")[0].Drop();

        Assert.Contains("can't be assigned", cut.Find(".alert-error").TextContent, StringComparison.Ordinal);
        Assert.Equal(["MOB-7", "MOB-4", "MOB-5"], Keys(Lane(cut, "Unassigned")));
    }
}
