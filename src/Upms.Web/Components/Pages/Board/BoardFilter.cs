using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.Board;

/// <summary>The board's filters as the address carries them (Phase 4 FR-047, research R19; contracts/ui-routes.md):
/// quick filters by label, type and epic or stage, and the assignee filter ("me", "unassigned" or a person). Each kind
/// matches any of its values; the kinds combine.</summary>
public sealed record BoardFilter
{
    public const string Me = "me";
    public const string Unassigned = "unassigned";

    public static BoardFilter None { get; } = new();

    public IReadOnlyList<string> Labels { get; init; } = [];

    public IReadOnlyList<WorkItemType> Kinds { get; init; } = [];

    /// <summary>Keys of epics or stages.</summary>
    public IReadOnlyList<string> Containers { get; init; } = [];

    public string Assignee { get; init; } = "";

    /// <summary>The swimlanes, kept in the address with the filters (Jira parity pass FR-026).</summary>
    public BoardGroup Group { get; init; }

    public bool IsActive => Labels.Count > 0 || Kinds.Count > 0 || Containers.Count > 0 || Assignee.Length > 0;

    /// <summary>The parameters as one string, to tell whether two filters are the same.</summary>
    public string Address => string.Join('&', ToParameters().Where(p => p.Value is not null).Select(p => $"{p.Key}={p.Value}"));

    /// <summary>The filters as the start of a query string ("kinds=bug&"), for links that keep them.</summary>
    public string QueryPrefix => string.Concat(ToParameters().Where(p => p.Value is not null)
        .Select(p => $"{p.Key}={Uri.EscapeDataString((string)p.Value!)}&"));

    /// <summary>Reads the address; values that mean nothing are dropped.</summary>
    public static BoardFilter Parse(string? labels, string? kinds, string? containers, string? assignee, string? group = null) => new()
    {
        Labels = Split(labels).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        Kinds = Split(kinds)
            .Select(k => Enum.TryParse<WorkItemType>(k, ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(k, out _)
                ? (WorkItemType?)kind
                : null)
            .OfType<WorkItemType>().Distinct().ToList(),
        Containers = Split(containers).Select(c => c.ToUpperInvariant()).Distinct().ToList(),
        Assignee = assignee is Me or Unassigned || Guid.TryParse(assignee, out _) ? assignee!.ToLowerInvariant() : "",
        Group = Enum.TryParse<BoardGroup>(group, ignoreCase: true, out var grouping) && Enum.IsDefined(grouping) && !int.TryParse(group, out _)
            ? grouping
            : BoardGroup.None,
    };

    public bool Matches(CardView card, Guid viewerId) =>
        (Labels.Count == 0 || card.Labels.Any(l => Labels.Contains(l, StringComparer.OrdinalIgnoreCase)))
        && (Kinds.Count == 0 || Kinds.Contains(card.Type))
        && (Containers.Count == 0 || (card.Container is { } container && Containers.Contains(container.Key)))
        && Assignee switch
        {
            Me => card.Assignee?.UserId == viewerId,
            Unassigned => card.Assignee is null,
            var id when Guid.TryParse(id, out var userId) => card.Assignee?.UserId == userId,
            _ => true,
        };

    public bool Has(string label) => Labels.Contains(label, StringComparer.OrdinalIgnoreCase);

    public BoardFilter ToggleLabel(string label) => this with { Labels = Toggle(Labels, label, StringComparer.OrdinalIgnoreCase) };

    public BoardFilter ToggleKind(WorkItemType kind) => this with { Kinds = Toggle(Kinds, kind, EqualityComparer<WorkItemType>.Default) };

    public BoardFilter ToggleContainer(string key) => this with { Containers = Toggle(Containers, key, StringComparer.OrdinalIgnoreCase) };

    public BoardFilter WithAssignee(string assignee) => this with { Assignee = assignee };

    public BoardFilter WithGroup(BoardGroup group) => this with { Group = group };

    /// <summary>The query string parameters; a filter not in use is removed from the address.</summary>
    public IReadOnlyDictionary<string, object?> ToParameters() => new Dictionary<string, object?>
    {
        ["labels"] = Labels.Count > 0 ? string.Join(',', Labels) : null,
        ["kinds"] = Kinds.Count > 0 ? string.Join(',', Kinds.Select(k => k.ToString().ToLowerInvariant())) : null,
        ["containers"] = Containers.Count > 0 ? string.Join(',', Containers) : null,
        ["assignee"] = Assignee.Length > 0 ? Assignee : null,
        ["group"] = Group == BoardGroup.None ? null : Group.ToString().ToLowerInvariant(),
    };

    private static string[] Split(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static List<T> Toggle<T>(IReadOnlyList<T> values, T value, IEqualityComparer<T> comparer) =>
        values.Contains(value, comparer) ? values.Where(v => !comparer.Equals(v, value)).ToList() : [.. values, value];
}
