using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A named search that belongs to one person (Phase 4 FR-039, FR-040, research R17). Its query is the search
/// page's address, so running it is opening that address; it can be shared with projects, and the sharing counts only
/// while the owner can still see them (checked when read).</summary>
public sealed class SavedFilter
{
    public const int NameMaxLength = 80;
    public const int QueryMaxLength = 2_000;

    private readonly List<SavedFilterShare> _shares = [];

    private SavedFilter()
    {
    }

    public long Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; } = "";

    /// <summary>Unique per owner (FR-039).</summary>
    public string NormalizedName { get; private set; } = "";

    /// <summary>The search page's canonical query string: words, filters and sort.</summary>
    public string Query { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<SavedFilterShare> Shares => _shares;

    public static string Normalize(string? name) => (name ?? "").Trim().ToUpperInvariant();

    public static DomainResult<SavedFilter> Create(Guid ownerId, string name, string query, DateTimeOffset now)
    {
        if ((CheckName(name) ?? CheckQuery(query)) is { } invalid)
        {
            return invalid;
        }

        return new SavedFilter
        {
            OwnerId = ownerId,
            Name = name.Trim(),
            NormalizedName = Normalize(name),
            Query = query,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public DomainError? Rename(string name, DateTimeOffset now)
    {
        if (CheckName(name) is { } invalid)
        {
            return invalid;
        }

        Name = name.Trim();
        NormalizedName = Normalize(name);
        UpdatedAt = now;
        return null;
    }

    /// <summary>Replaces the search with the current one (FR-039).</summary>
    public DomainError? Update(string query, DateTimeOffset now)
    {
        if (CheckQuery(query) is { } invalid)
        {
            return invalid;
        }

        Query = query;
        UpdatedAt = now;
        return null;
    }

    /// <summary>Shares with exactly these projects; projects already shared keep their time (FR-040).</summary>
    public void ShareWith(IReadOnlyCollection<long> projectIds, DateTimeOffset now)
    {
        var wanted = projectIds.ToHashSet();
        _shares.RemoveAll(s => !wanted.Contains(s.ProjectId));
        foreach (var projectId in wanted.Where(id => _shares.All(s => s.ProjectId != id)))
        {
            _shares.Add(new SavedFilterShare(projectId, now));
        }
    }

    private static DomainError? CheckName(string? name) => (name ?? "").Trim().Length switch
    {
        0 => DomainError.Invalid("Name", "Give the filter a name."),
        > NameMaxLength => DomainError.Invalid("Name", $"A filter's name can have at most {NameMaxLength} characters."),
        _ => null,
    };

    private static DomainError? CheckQuery(string? query) => (query ?? "").Length > QueryMaxLength
        ? DomainError.Invalid("Query", "This search has too many filters to save.")
        : null;
}

/// <summary>A saved filter shared with the members of a project.</summary>
public sealed class SavedFilterShare
{
    private SavedFilterShare()
    {
    }

    internal SavedFilterShare(long projectId, DateTimeOffset sharedAt)
    {
        ProjectId = projectId;
        SharedAt = sharedAt;
    }

    public long SavedFilterId { get; private set; }

    public long ProjectId { get; private set; }

    public DateTimeOffset SharedAt { get; private set; }
}
