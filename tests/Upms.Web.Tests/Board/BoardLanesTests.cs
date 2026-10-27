using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Board;

/// <summary>Swimlanes grouped from the cards the board already has (Jira parity pass FR-026; research R16).</summary>
public sealed class BoardLanesTests
{
    private static readonly AssigneeRef Sara = AssigneeRef.Of(Guid.NewGuid(), "Sara Khan", true);
    private static readonly AssigneeRef Bilal = AssigneeRef.Of(Guid.NewGuid(), "Bilal Ahmed", true);

    private static ContainerView Epic(string key, string title, EpicColor color = EpicColor.Blue) => new(key, title, WorkItemType.Epic, color);

    private static CardView Card(string key, ContainerView? container = null, AssigneeRef? assignee = null, string? parentKey = null)
    {
        var type = parentKey is null ? WorkItemType.Task : WorkItemType.Subtask;
        var card = FakeBoardService.Card(key, $"Card {key}", Priority.Medium, FakeBoardService.ToDo, assignee, type: type);
        return card with { Container = container, ParentKey = parentKey };
    }

    private static List<(string Title, int Count)> Summary(IReadOnlyList<BoardLane> lanes) =>
        lanes.Select(l => (l.Title, l.Cards.Count)).ToList();

    [Fact]
    public void JP_US5_AS3_Epic_lanes_come_in_key_order_with_no_epic_last()
    {
        var refunds = Epic("WEB-10", "Card refunds");
        var wallet = Epic("WEB-2", "Wallet", EpicColor.Teal);
        var cards = new[] { Card("WEB-11", refunds), Card("WEB-12"), Card("WEB-3", wallet), Card("WEB-13", refunds) };

        var lanes = BoardLanes.Group(cards, BoardGroup.Epic, ProjectTemplate.Kanban);

        Assert.Equal([("WEB-2 Wallet", 1), ("WEB-10 Card refunds", 2), ("No epic", 1)], Summary(lanes));
        Assert.Equal(new LaneChange.Container("WEB-2"), lanes[0].Change);
        Assert.Equal(new LaneChange.Container(null), lanes[2].Change);
        Assert.Equal((EpicColor?)EpicColor.Teal, lanes[0].Color);
    }

    [Fact]
    public void Stage_gate_boards_group_by_stage()
    {
        var planning = new ContainerView("CBU-2", "Planning", WorkItemType.Stage);

        var lanes = BoardLanes.Group([Card("CBU-5", planning), Card("CBU-6")], BoardGroup.Epic, ProjectTemplate.StageGate);

        Assert.Equal([("CBU-2 Planning", 1), ("No stage", 1)], Summary(lanes));
    }

    [Fact]
    public void Assignee_lanes_are_by_name_with_unassigned_last()
    {
        var lanes = BoardLanes.Group([Card("WEB-1", assignee: Sara), Card("WEB-2"), Card("WEB-3", assignee: Bilal)], BoardGroup.Assignee,
            ProjectTemplate.Kanban);

        Assert.Equal([("Bilal Ahmed", 1), ("Sara Khan", 1), ("Unassigned", 1)], Summary(lanes));
        Assert.Equal(new LaneChange.Assignee(Bilal.UserId), lanes[0].Change);
        Assert.Equal(new LaneChange.Assignee(null), lanes[2].Change);
    }

    [Fact]
    public void Sub_task_lanes_hold_each_parents_sub_tasks_and_everything_else_last()
    {
        var cards = new[] { Card("WEB-1"), Card("WEB-2"), Card("WEB-5", parentKey: "WEB-2"), Card("WEB-6", parentKey: "WEB-2") };

        var lanes = BoardLanes.Group(cards, BoardGroup.Subtasks, ProjectTemplate.Kanban);

        Assert.Equal([("WEB-2 Card WEB-2", 2), ("Everything else", 2)], Summary(lanes));
        Assert.All(lanes, l => Assert.Null(l.Change));
        Assert.Equal(["WEB-1", "WEB-2"], lanes[1].Cards.Select(c => c.Key));
    }

    [Fact]
    public void A_sub_task_card_takes_its_parents_epic_lane()
    {
        var refunds = Epic("WEB-1", "Card refunds");

        var lanes = BoardLanes.Group([Card("WEB-2", refunds), Card("WEB-3", refunds, parentKey: "WEB-2")], BoardGroup.Epic, ProjectTemplate.Kanban);

        Assert.Equal([("WEB-1 Card refunds", 2)], Summary(lanes));
    }

    [Fact]
    public void Lanes_past_twenty_start_collapsed()
    {
        var cards = Enumerable.Range(1, 22).Select(n => Card($"WEB-{100 + n}", Epic($"WEB-{n}", $"Epic {n}"))).Append(Card("WEB-200"));

        var lanes = BoardLanes.Group(cards.ToList(), BoardGroup.Epic, ProjectTemplate.Kanban);

        Assert.Equal(23, lanes.Count);
        Assert.All(lanes.Take(BoardLanes.OpenLanes), l => Assert.False(l.StartsCollapsed));
        Assert.All(lanes.Skip(BoardLanes.OpenLanes), l => Assert.True(l.StartsCollapsed));
    }

    [Fact]
    public void No_cards_make_no_lanes()
    {
        Assert.Empty(BoardLanes.Group([], BoardGroup.Assignee, ProjectTemplate.Kanban));
    }
}
