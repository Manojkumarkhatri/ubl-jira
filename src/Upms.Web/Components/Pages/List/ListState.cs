using System.Globalization;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.List;

/// <summary>The List view's sort, filters and page as they appear in its address, so a list can be bookmarked, shared
/// and reloaded (Phase 2 FR-031; contracts/ui-routes.md): <c>sort</c>, <c>dir</c>, <c>column</c>, <c>type</c> (the
/// status type), <c>priority</c>, <c>assignee</c> (<c>me</c>, <c>none</c> or IDs), <c>due</c> (<c>overdue</c>,
/// <c>week</c>, <c>none</c>), <c>q</c>, <c>page</c>, and since Phase 3 <c>kind</c> (work item types) and
/// <c>container</c> (epic or stage keys, or <c>none</c>) (FR-020), and since Phase 4 <c>labels</c> (items with any of them,
/// FR-046). Lists are comma-separated, and values that mean nothing are ignored. <c>type</c> keeps its Phase 2 meaning so that saved addresses still work.</summary>
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

    /// <summary>The <c>container</c> value for items in no epic or stage.</summary>
    public const string NoContainerValue = "none";

    /// <summary>Only these work item types (Phase 3 FR-020).</summary>
    public IReadOnlyList<WorkItemType> ItemTypes { get; init; } = [];

    /// <summary>Only items in these epics or stages, by key.</summary>
    public IReadOnlyList<string> Containers { get; init; } = [];

    /// <summary>Items in no epic or stage.</summary>
    public bool NoContainer { get; init; }

    /// <summary>Items with any of these labels (Phase 4 FR-046); kept as typed, matched regardless of case.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

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
        [ListSort.Type] = "type",
        [ListSort.Created] = "created",
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
    private static readonly Dictionary<string, WorkItemType> ItemTypesByName =
        Enum.GetValues<WorkItemType>().ToDictionary(t => NameOf(t));

    /// <summary>Every task, newest key first (FR-029).</summary>
    public static ListState Default { get; } = new(ListSort.Key, true, [], [], [], false, false, [], DueFilter.Any, "", 1);

    public bool HasFilters =>
        Columns.Count > 0 || Types.Count > 0 || Priorities.Count > 0 || Me || Unassigned || People.Count > 0
        || Due != DueFilter.Any || Text.Length > 0 || ItemTypes.Count > 0 || Containers.Count > 0 || NoContainer || Labels.Count > 0;

    public static string NameOf(ListSort sort) => SortNames[sort];

    public static string NameOf(StatusCategory type) => TypeNames[type];

    public static string NameOf(DueFilter due) => DueNames.GetValueOrDefault(due, "");

    /// <summary>A work item type as the <c>kind</c> value: "story", "subtask".</summary>
    public static string NameOf(WorkItemType type) => type.ToString().ToLowerInvariant();

    public static ListState Parse(string? sort, string? dir, string? column, string? type, string? priority, string? assignee,
        string? due, string? q, int? page, string? kind = null, string? container = null, string? labels = null)
    {
        var parsedSort = SortsByName.GetValueOrDefault(sort?.Trim().ToLowerInvariant() ?? "", ListSort.Key);
        var assignees = Values(assignee).ToList();
        var containers = Values(container).ToList();
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
            Math.Max(1, page ?? 1))
        {
            ItemTypes = Values(kind).Where(ItemTypesByName.ContainsKey).Select(v => ItemTypesByName[v]).Distinct().ToList(),
            // Keys are upper case (FR-012); anything that cannot be a key is left out.
            Containers = containers.Where(v => v != NoContainerValue).Select(v => v.ToUpperInvariant())
                .Where(v => v.Length <= WorkItem.KeyMaxLength && v.Contains('-', StringComparison.Ordinal)).Distinct().ToList(),
            NoContainer = containers.Contains(NoContainerValue),
            Labels = (labels ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    public WorkItemListQuery ToQuery() => new(Sort, Descending, Columns, Types, Priorities, Me, Unassigned, People, Due,
        Text.Length == 0 ? null : Text, Page, ItemTypes, Containers, NoContainer, Labels);

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
        ["kind"] = Join(ItemTypes.Select(NameOf)),
        ["container"] = Join(Containers.Concat(NoContainer ? [NoContainerValue] : [])),
        ["labels"] = Join(Labels),
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

    public ListState WithItemType(WorkItemType? type) => this with { ItemTypes = type is { } t ? [t] : [], Page = 1 };

    /// <summary>An epic or stage key, <c>none</c>, or empty for any.</summary>
    public ListState WithContainer(string value) => this with
    {
        Containers = value.Length > 0 && value != NoContainerValue ? [value] : [],
        NoContainer = value == NoContainerValue,
        Page = 1,
    };

    /// <summary>Adds the label to the filter, or takes it out if it is there (any case).</summary>
    public ListState ToggleLabel(string label) => this with
    {
        Labels = Labels.Contains(label, StringComparer.OrdinalIgnoreCase)
            ? Labels.Where(l => !l.Equals(label, StringComparison.OrdinalIgnoreCase)).ToList()
            : [.. Labels, label.Trim()],
        Page = 1,
    };

    // Jira's Filter panel and person avatars: each choice is added to the filter, or taken out if it is there.
    public ListState ToggleColumn(long id) => this with { Columns = Flip(Columns, id), Page = 1 };

    public ListState ToggleType(StatusCategory type) => this with { Types = Flip(Types, type), Page = 1 };

    public ListState TogglePriority(Priority priority) => this with { Priorities = Flip(Priorities, priority), Page = 1 };

    public ListState ToggleItemType(WorkItemType type) => this with { ItemTypes = Flip(ItemTypes, type), Page = 1 };

    public ListState ToggleContainer(string key) => this with { Containers = Flip(Containers, key), Page = 1 };

    public ListState ToggleNoContainer() => this with { NoContainer = !NoContainer, Page = 1 };

    public ListState ToggleMe() => this with { Me = !Me, Page = 1 };

    public ListState ToggleUnassigned() => this with { Unassigned = !Unassigned, Page = 1 };

    public ListState TogglePerson(Guid id) => this with { People = Flip(People, id), Page = 1 };

    /// <summary>How many filter values are chosen, for the Filter button (the words and people are counted apart).</summary>
    public int FilterCount => Columns.Count + Types.Count + Priorities.Count + ItemTypes.Count + Containers.Count + (NoContainer ? 1 : 0)
        + Labels.Count + (Due == DueFilter.Any ? 0 : 1);

    private static List<T> Flip<T>(IReadOnlyList<T> values, T value) =>
        values.Contains(value) ? values.Where(v => !EqualityComparer<T>.Default.Equals(v, value)).ToList() : [.. values, value];

    public ListState WithText(string text) => this with { Text = text.Trim(), Page = 1 };

    public ListState WithoutFilters() => Default with { Sort = Sort, Descending = Descending };

    public ListState WithPage(int page) => this with { Page = Math.Max(1, page) };

    // Keys and last updates read best newest first; everything else starts ascending.
    private static bool DescendingByDefault(ListSort sort) => sort is ListSort.Key or ListSort.Updated or ListSort.Created;

    private static IEnumerable<string> Values(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => v.ToLowerInvariant());

    private static string? Join(IEnumerable<string> values)
    {
        var joined = string.Join(',', values);
        return joined.Length == 0 ? null : joined;
    }
}
