using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.Search;

/// <summary>The search page's words, filters, sort and page as they appear in its address (Phase 4 contracts/ui-routes.md),
/// so a search can be bookmarked, shared and saved: <c>q</c>, <c>project</c> (keys), <c>kind</c>, <c>category</c>,
/// <c>priority</c>, <c>assignee</c> (<c>me</c>, <c>none</c> or IDs), <c>creator</c> (<c>me</c> or IDs), <c>label</c>,
/// <c>created</c>, <c>updated</c> and <c>due</c> (<c>from..to</c>, either side open), <c>sort</c>, <c>desc</c>,
/// <c>page</c> and <c>filter</c> (a saved filter's ID). Lists are comma-separated; values that mean nothing are
/// ignored.</summary>
public sealed record SearchState
{
    public const string MeValue = "me";
    public const string UnassignedValue = "none";

    private static readonly Dictionary<SearchSort, string> SortNames = new()
    {
        [SearchSort.Updated] = "updated",
        [SearchSort.Created] = "created",
        [SearchSort.Key] = "key",
        [SearchSort.Priority] = "priority",
        [SearchSort.DueDate] = "due",
    };

    private static readonly Dictionary<StatusCategory, string> CategoryNames = new()
    {
        [StatusCategory.ToDo] = "todo",
        [StatusCategory.InProgress] = "inprogress",
        [StatusCategory.Done] = "done",
    };

    public static SearchState Default { get; } = new();

    public string Words { get; init; } = "";

    public IReadOnlyList<string> Projects { get; init; } = [];

    public IReadOnlyList<WorkItemType> Kinds { get; init; } = [];

    public IReadOnlyList<StatusCategory> Categories { get; init; } = [];

    public IReadOnlyList<Priority> Priorities { get; init; } = [];

    public bool Me { get; init; }

    public bool Unassigned { get; init; }

    public IReadOnlyList<Guid> People { get; init; } = [];

    public bool CreatedByMe { get; init; }

    public IReadOnlyList<Guid> Creators { get; init; } = [];

    public IReadOnlyList<string> Labels { get; init; } = [];

    public DateRange? Created { get; init; }

    public DateRange? Updated { get; init; }

    public DateRange? Due { get; init; }

    /// <summary>When items were completed (Jira parity pass FR-022: the Summary page's "completed" count opens it).</summary>
    public DateRange? Resolved { get; init; }

    public SearchSort Sort { get; init; } = SearchSort.Updated;

    public bool Descending { get; init; } = true;

    public int Page { get; init; } = 1;

    /// <summary>The saved filter this search came from, if any.</summary>
    public long? FilterId { get; init; }

    /// <summary>Anything asked for besides the order: words or a filter.</summary>
    public bool HasCriteria => Words.Length > 0 || Projects.Count > 0 || Kinds.Count > 0 || Categories.Count > 0 || Priorities.Count > 0
        || Me || Unassigned || People.Count > 0 || CreatedByMe || Creators.Count > 0 || Labels.Count > 0
        || Created is not null || Updated is not null || Due is not null || Resolved is not null;

    public static string NameOf(SearchSort sort) => SortNames[sort];

    public static string NameOf(StatusCategory category) => CategoryNames[category];

    public static string NameOf(WorkItemType type) => type.ToString().ToLowerInvariant();

    /// <summary>A query string, with or without its leading "?".</summary>
    public static SearchState Parse(string? queryString)
    {
        var values = QueryHelpers.ParseQuery(queryString ?? "").ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        string? Value(string name) => values.GetValueOrDefault(name);
        var assignees = List(Value("assignee")).ToList();
        var creators = List(Value("creator")).ToList();
        var sort = SortNames.FirstOrDefault(s => s.Value == Value("sort")?.Trim().ToLowerInvariant()).Key;
        return new SearchState
        {
            Words = (Value("q") ?? "").Trim(),
            // "space" since the Jira parity pass; "project" is still read, so older links and saved filters keep working (FR-020).
            Projects = List(Value("space") ?? Value("project")).Select(k => k.ToUpperInvariant()).Distinct().ToList(),
            Kinds = List(Value("kind")).SelectMany(v => Enum.GetValues<WorkItemType>().Where(t => NameOf(t) == v)).Distinct().ToList(),
            Categories = List(Value("category")).SelectMany(v => CategoryNames.Where(c => c.Value == v).Select(c => c.Key)).Distinct().ToList(),
            Priorities = List(Value("priority")).SelectMany(v => Enum.GetValues<Priority>().Where(p => p.ToString().Equals(v, StringComparison.OrdinalIgnoreCase)))
                .Distinct().ToList(),
            Me = assignees.Contains(MeValue),
            Unassigned = assignees.Contains(UnassignedValue),
            People = Ids(assignees),
            CreatedByMe = creators.Contains(MeValue),
            Creators = Ids(creators),
            Labels = List(Value("label"), lowerCase: false).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Created = Range(Value("created")),
            Updated = Range(Value("updated")),
            Due = Range(Value("due")),
            Resolved = Range(Value("resolved")),
            Sort = sort,
            Descending = Value("desc")?.Trim().ToLowerInvariant() switch
            {
                "false" => false,
                "true" => true,
                _ => sort is SearchSort.Updated or SearchSort.Created,
            },
            Page = int.TryParse(Value("page"), NumberStyles.None, CultureInfo.InvariantCulture, out var page) ? Math.Max(1, page) : 1,
            FilterId = long.TryParse(Value("filter"), NumberStyles.None, CultureInfo.InvariantCulture, out var filter) && filter > 0 ? filter : null,
        };
    }

    /// <summary>The query for the search service; "me" is the viewer.</summary>
    public SearchQuery ToQuery(Guid? me) => new(
        Words.Length == 0 ? null : Words,
        Projects,
        Kinds,
        Categories,
        Priorities,
        Me && me is not null,
        Unassigned,
        People,
        CreatedByMe && me is { } creator ? [.. Creators, creator] : Creators,
        Labels,
        Created,
        Updated,
        Due,
        Sort,
        Descending,
        Resolved);

    /// <summary>The address's query parameters; defaults are left out (null).</summary>
    public IReadOnlyDictionary<string, object?> ToParameters() => new Dictionary<string, object?>(Criteria())
    {
        ["page"] = Page <= 1 ? null : Page,
        ["filter"] = FilterId,
    };

    /// <summary>The words, filters and sort as one canonical query string: what a saved filter keeps (research R17).</summary>
    public string ToQueryString() => string.Join('&', Criteria()
        .Where(p => p.Value is not null)
        .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, CultureInfo.InvariantCulture)!)}"));

    public SearchState WithWords(string words) => this with { Words = words.Trim(), Page = 1 };

    public SearchState WithProject(string? key) => this with { Projects = string.IsNullOrWhiteSpace(key) ? [] : [key.Trim().ToUpperInvariant()], Page = 1 };

    public SearchState WithKind(WorkItemType? kind) => this with { Kinds = kind is { } k ? [k] : [], Page = 1 };

    public SearchState WithCategory(StatusCategory? category) => this with { Categories = category is { } c ? [c] : [], Page = 1 };

    public SearchState WithPriority(Priority? priority) => this with { Priorities = priority is { } p ? [p] : [], Page = 1 };

    /// <summary><c>me</c>, <c>none</c>, a person's ID, or empty for anyone.</summary>
    public SearchState WithAssignee(string value) => this with
    {
        Me = value == MeValue,
        Unassigned = value == UnassignedValue,
        People = Guid.TryParse(value, out var id) ? [id] : [],
        Page = 1,
    };

    public SearchState WithCreatedByMe(bool mine) => this with { CreatedByMe = mine, Creators = [], Page = 1 };

    public SearchState WithLabels(IReadOnlyList<string> labels) => this with { Labels = labels, Page = 1 };

    public SearchState WithCreated(DateRange? range) => this with { Created = Normalize(range), Page = 1 };

    public SearchState WithUpdated(DateRange? range) => this with { Updated = Normalize(range), Page = 1 };

    public SearchState WithDue(DateRange? range) => this with { Due = Normalize(range), Page = 1 };

    public SearchState WithResolved(DateRange? range) => this with { Resolved = Normalize(range), Page = 1 };

    /// <summary>Sorts by the choice; choosing the current one again reverses the order.</summary>
    public SearchState WithSort(SearchSort sort) =>
        this with { Sort = sort, Descending = sort == Sort ? !Descending : sort is SearchSort.Updated or SearchSort.Created, Page = 1 };

    public SearchState WithPage(int page) => this with { Page = Math.Max(1, page) };

    private Dictionary<string, object?> Criteria() => new()
    {
        ["q"] = Words.Length == 0 ? null : Words,
        ["space"] = Join(Projects),
        ["kind"] = Join(Kinds.Select(NameOf)),
        ["category"] = Join(Categories.Select(NameOf)),
        ["priority"] = Join(Priorities.Select(p => p.ToString().ToLowerInvariant())),
        ["assignee"] = Join((Me ? [MeValue] : Array.Empty<string>()).Concat(Unassigned ? [UnassignedValue] : []).Concat(People.Select(p => p.ToString()))),
        ["creator"] = Join((CreatedByMe ? [MeValue] : Array.Empty<string>()).Concat(Creators.Select(c => c.ToString()))),
        ["label"] = Join(Labels),
        ["created"] = Format(Created),
        ["updated"] = Format(Updated),
        ["due"] = Format(Due),
        ["resolved"] = Format(Resolved),
        ["sort"] = Sort == SearchSort.Updated ? null : SortNames[Sort],
        ["desc"] = Descending == (Sort is SearchSort.Updated or SearchSort.Created) ? null : Descending ? "true" : "false",
    };

    private static DateRange? Normalize(DateRange? range) => range is { From: null, To: null } ? null : range;

    private static IEnumerable<string> List(string? text, bool lowerCase = true) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => lowerCase ? v.ToLowerInvariant() : v);

    private static List<Guid> Ids(IEnumerable<string> values) =>
        values.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();

    /// <summary><c>2026-10-01..2026-10-31</c>, <c>2026-10-01..</c> or <c>..2026-10-31</c>.</summary>
    private static DateRange? Range(string? text)
    {
        var parts = (text ?? "").Split("..", 2);
        if (parts.Length != 2)
        {
            return null;
        }

        DateOnly? Date(string part) =>
            DateOnly.TryParseExact(part.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
        return Normalize(new DateRange(Date(parts[0]), Date(parts[1])));
    }

    private static string? Format(DateRange? range) => range is null
        ? null
        : $"{range.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}..{range.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private static string? Join(IEnumerable<string> values)
    {
        var joined = string.Join(',', values);
        return joined.Length == 0 ? null : joined;
    }
}
