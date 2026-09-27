using System.Globalization;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.List;

/// <summary>The List view's sort, filters and page as they appear in its address, so a list can be bookmarked, shared
/// and reloaded (Phase 2 FR-031; contracts/ui-routes.md): <c>sort</c>, <c>dir</c>, <c>column</c>, <c>type</c>,
/// <c>priority</c>, <c>assignee</c> (<c>me</c>, <c>none</c> or IDs), <c>due</c> (<c>overdue</c>, <c>week</c>,
/// <c>none</c>), <c>q</c> and <c>page</c>. Lists are comma-separated, and values that mean nothing are ignored.</summary>
public sealed record ListState(
    ListSort Sort,
    bool Descending,
    IReadOnlyList<long> Columns,
    IReadOnlyList<StatusCategory> Types,
    IReadOnlyList<Priority> Priorities,
    bool Me,
    bool Unassigned,
    IReadOnlyList<Guid> People,
    DueFilter Due,
    string Text,
    int Page)
{
    public const string MeValue = "me";
    public const string UnassignedValue = "none";

    private static readonly Dictionary<ListSort, string> SortNames = new()
    {
        [ListSort.Key] = "key",
        [ListSort.Title] = "title",
        [ListSort.Status] = "status",
        [ListSort.Priority] = "priority",
        [ListSort.Assignee] = "assignee",
        [ListSort.StartDate] = "start",
        [ListSort.DueDate] = "due",
        [ListSort.Updated] = "updated",
    };

    private static readonly Dictionary<StatusCategory, string> TypeNames = new()
    {
        [StatusCategory.ToDo] = "todo",
        [StatusCategory.InProgress] = "inprogress",
        [StatusCategory.Done] = "done",
    };

    private static readonly Dictionary<DueFilter, string> DueNames = new()
    {
        [DueFilter.Overdue] = "overdue",
        [DueFilter.Next7Days] = "week",
        [DueFilter.NoDueDate] = "none",
    };

    private static readonly Dictionary<string, ListSort> SortsByName = SortNames.ToDictionary(s => s.Value, s => s.Key);
    private static readonly Dictionary<string, StatusCategory> TypesByName = TypeNames.ToDictionary(t => t.Value, t => t.Key);
    private static readonly Dictionary<string, DueFilter> DuesByName = DueNames.ToDictionary(d => d.Value, d => d.Key);
    private static readonly Dictionary<string, Priority> PrioritiesByName =
        Enum.GetValues<Priority>().ToDictionary(p => p.ToString().ToLowerInvariant());

    /// <summary>Every task, newest key first (FR-029).</summary>
    public static ListState Default { get; } = new(ListSort.Key, true, [], [], [], false, false, [], DueFilter.Any, "", 1);

    public bool HasFilters =>
        Columns.Count > 0 || Types.Count > 0 || Priorities.Count > 0 || Me || Unassigned || People.Count > 0
        || Due != DueFilter.Any || Text.Length > 0;

    public static string NameOf(ListSort sort) => SortNames[sort];

    public static string NameOf(StatusCategory type) => TypeNames[type];

    public static string NameOf(DueFilter due) => DueNames.GetValueOrDefault(due, "");

    public static ListState Parse(string? sort, string? dir, string? column, string? type, string? priority, string? assignee,
        string? due, string? q, int? page)
    {
        var parsedSort = SortsByName.GetValueOrDefault(sort?.Trim().ToLowerInvariant() ?? "", ListSort.Key);
        var assignees = Values(assignee).ToList();
        return new ListState(
            parsedSort,
            dir?.Trim().ToLowerInvariant() switch
            {
                "asc" => false,
                "desc" => true,
                _ => DescendingByDefault(parsedSort),
            },
            Values(column).Select(v => long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0).Distinct().ToList(),
            Values(type).Where(TypesByName.ContainsKey).Select(v => TypesByName[v]).Distinct().ToList(),
            Values(priority).Where(PrioritiesByName.ContainsKey).Select(v => PrioritiesByName[v]).Distinct().ToList(),
            assignees.Contains(MeValue),
            assignees.Contains(UnassignedValue),
            assignees.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList(),
            DuesByName.GetValueOrDefault(due?.Trim().ToLowerInvariant() ?? "", DueFilter.Any),
            (q ?? "").Trim(),
            Math.Max(1, page ?? 1));
    }

    public WorkItemListQuery ToQuery() => new(Sort, Descending, Columns, Types, Priorities, Me, Unassigned, People, Due,
        Text.Length == 0 ? null : Text, Page);

    /// <summary>The address's query parameters; values equal to the default are left out (null).</summary>
    public IReadOnlyDictionary<string, object?> ToParameters() => new Dictionary<string, object?>
    {
        ["sort"] = Sort == ListSort.Key ? null : SortNames[Sort],
        ["dir"] = Descending == DescendingByDefault(Sort) ? null : Descending ? "desc" : "asc",
        ["column"] = Join(Columns.Select(c => c.ToString(CultureInfo.InvariantCulture))),
        ["type"] = Join(Types.Select(t => TypeNames[t])),
        ["priority"] = Join(Priorities.Select(p => p.ToString().ToLowerInvariant())),
        ["assignee"] = Join((Me ? [MeValue] : Array.Empty<string>())
            .Concat(Unassigned ? [UnassignedValue] : [])
            .Concat(People.Select(p => p.ToString()))),
        ["due"] = Due == DueFilter.Any ? null : DueNames[Due],
        ["q"] = Text.Length == 0 ? null : Text,
        ["page"] = Page <= 1 ? null : Page,
    };

    /// <summary>Sorts by the column; choosing the current column again reverses the order.</summary>
    public ListState WithSort(ListSort sort) =>
        this with { Sort = sort, Descending = sort == Sort ? !Descending : DescendingByDefault(sort), Page = 1 };

    public ListState WithColumn(long? columnId) => this with { Columns = columnId is { } id ? [id] : [], Page = 1 };

    public ListState WithType(StatusCategory? type) => this with { Types = type is { } t ? [t] : [], Page = 1 };

    public ListState WithPriority(Priority? priority) => this with { Priorities = priority is { } p ? [p] : [], Page = 1 };

    /// <summary><c>me</c>, <c>none</c>, a person's ID, or empty for anyone.</summary>
    public ListState WithAssignee(string value) => this with
    {
        Me = value == MeValue,
        Unassigned = value == UnassignedValue,
        People = Guid.TryParse(value, out var id) ? [id] : [],
        Page = 1,
    };

    public ListState WithDue(DueFilter due) => this with { Due = due, Page = 1 };

    public ListState WithText(string text) => this with { Text = text.Trim(), Page = 1 };

    public ListState WithoutFilters() => Default with { Sort = Sort, Descending = Descending };

    public ListState WithPage(int page) => this with { Page = Math.Max(1, page) };

    // Keys and last updates read best newest first; everything else starts ascending.
    private static bool DescendingByDefault(ListSort sort) => sort is ListSort.Key or ListSort.Updated;

    private static IEnumerable<string> Values(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => v.ToLowerInvariant());

    private static string? Join(IEnumerable<string> values)
    {
        var joined = string.Join(',', values);
        return joined.Length == 0 ? null : joined;
    }
}
