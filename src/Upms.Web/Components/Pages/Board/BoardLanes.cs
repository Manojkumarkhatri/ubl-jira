using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.Board;

/// <summary>How the board is split into swimlanes, as Jira's "Group by" (Jira parity pass FR-026).</summary>
public enum BoardGroup
{
    None,

    /// <summary>By epic, or by stage on Stage-gate boards.</summary>
    Epic,

    Assignee,

    /// <summary>Each parent's sub-tasks, then everything else.</summary>
    Subtasks,

    /// <summary>A lane per label, in name order; a card with several labels shows in each, as in Jira.</summary>
    Labels,

    /// <summary>Highest to lowest; a card dropped in another lane takes its priority.</summary>
    Priority,
}

/// <summary>A swimlane: its cards in board order, and what a card dropped into it changes (none for sub-task lanes).</summary>
/// <param name="Color">An epic lane's color, for its tag.</param>
/// <param name="StartsCollapsed">Lanes past the first <see cref="BoardLanes.OpenLanes"/> start collapsed.</param>
/// <param name="IsLabel">A label's lane, whose title shows as a label chip.</param>
public sealed record BoardLane(string Id, string Title, IReadOnlyList<CardView> Cards, LaneChange? Change, EpicColor? Color = null,
    bool StartsCollapsed = false, bool IsLabel = false);

/// <summary>Groups the cards the board already shows into lanes, so grouping needs no extra read (research R16). The quick
/// filters apply first; lanes come in key (or name) order with "No epic", "No stage", "Unassigned" or "Everything else"
/// last.</summary>
public static class BoardLanes
{
    public const int OpenLanes = 20;

    public static IReadOnlyList<BoardLane> Group(IReadOnlyList<CardView> cards, BoardGroup group, ProjectTemplate template)
    {
        var lanes = group switch
        {
            BoardGroup.Epic => ByContainer(cards, template),
            BoardGroup.Assignee => ByAssignee(cards),
            BoardGroup.Subtasks => ByParent(cards),
            BoardGroup.Labels => ByLabel(cards),
            BoardGroup.Priority => ByPriority(cards),
            _ => [],
        };
        return lanes.Select((lane, index) => lane with { StartsCollapsed = index >= OpenLanes }).ToList();
    }

    /// <summary>"No epic", or "No stage" on Stage-gate boards.</summary>
    public static string NoContainer(ProjectTemplate template) => template == ProjectTemplate.StageGate ? "No stage" : "No epic";

    private static List<BoardLane> ByContainer(IReadOnlyList<CardView> cards, ProjectTemplate template)
    {
        var lanes = cards.Where(c => c.Container is not null)
            .GroupBy(c => c.Container!.Key)
            .OrderBy(g => NumberOf(g.Key))
            .Select(g =>
            {
                var container = g.First().Container!;
                return new BoardLane($"lane-{g.Key}", $"{container.Key} {container.Title}", g.ToList(), new LaneChange.Container(g.Key),
                    container.Color);
            })
            .ToList();
        return WithRest(lanes, cards.Where(c => c.Container is null).ToList(), "lane-none", NoContainer(template),
            new LaneChange.Container(null));
    }

    private static List<BoardLane> ByAssignee(IReadOnlyList<CardView> cards)
    {
        var lanes = cards.Where(c => c.Assignee is not null)
            .GroupBy(c => c.Assignee!.UserId)
            .Select(g => (Person: g.First().Assignee!, Cards: g.ToList()))
            .OrderBy(p => p.Person.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new BoardLane($"lane-{p.Person.UserId:N}", p.Person.DisplayName, p.Cards, new LaneChange.Assignee(p.Person.UserId)))
            .ToList();
        return WithRest(lanes, cards.Where(c => c.Assignee is null).ToList(), "lane-none", "Unassigned", new LaneChange.Assignee(null));
    }

    private static List<BoardLane> ByParent(IReadOnlyList<CardView> cards)
    {
        var titles = cards.Where(c => c.ParentKey is null).ToDictionary(c => c.Key, c => c.Title);
        var lanes = cards.Where(c => c.ParentKey is not null)
            .GroupBy(c => c.ParentKey!)
            .OrderBy(g => NumberOf(g.Key))
            .Select(g => new BoardLane($"lane-{g.Key}", titles.TryGetValue(g.Key, out var title) ? $"{g.Key} {title}" : g.Key, g.ToList(), null))
            .ToList();
        return WithRest(lanes, cards.Where(c => c.ParentKey is null).ToList(), "lane-none", "Everything else", null);
    }

    private static List<BoardLane> ByLabel(IReadOnlyList<CardView> cards)
    {
        var names = cards.SelectMany(c => c.Labels).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var lanes = names.Select((name, index) => new BoardLane($"lane-label-{index}", name,
                cards.Where(c => c.Labels.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList(), null, IsLabel: true))
            .ToList();
        return WithRest(lanes, cards.Where(c => c.Labels.Count == 0).ToList(), "lane-none", "No labels", null);
    }

    private static List<BoardLane> ByPriority(IReadOnlyList<CardView> cards) =>
        Enum.GetValues<Priority>()
            .Select(p => (Priority: p, Cards: cards.Where(c => c.Priority == p).ToList()))
            .Where(p => p.Cards.Count > 0)
            .Select(p => new BoardLane($"lane-priority-{p.Priority.ToString().ToLowerInvariant()}", p.Priority.ToString(), p.Cards,
                new LaneChange.Priority(p.Priority)))
            .ToList();

    private static List<BoardLane> WithRest(List<BoardLane> lanes, List<CardView> rest, string id, string title, LaneChange? change)
    {
        if (rest.Count > 0)
        {
            lanes.Add(new BoardLane(id, title, rest, change));
        }

        return lanes;
    }

    /// <summary>The number in a key ("WEB-10" is 10), so WEB-2 comes before WEB-10.</summary>
    private static int NumberOf(string key) =>
        int.TryParse(key.AsSpan(key.LastIndexOf('-') + 1), out var number) ? number : int.MaxValue;
}
