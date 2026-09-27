namespace Upms.Domain.Projects;

/// <summary>A body of work with its own board (data-model.md, "Project").</summary>
public sealed class Project
{
    public const int KeyMinLength = 2;
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectStatus> _statuses = [];

    private Project()
    {
    }

    /// <summary>Creates a project row without validation or default columns. Used by test builders
    /// until <c>Project.Create</c> arrives with User Story 1 (tasks.md T024, T065).</summary>
    public Project(string key, string name, string? description, Guid ownerId, DateTimeOffset createdAt)
    {
        Key = key;
        Name = name;
        NormalizedName = NormalizeName(name);
        Description = description;
        OwnerId = ownerId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public long Id { get; private set; }

    /// <summary>Unique, immutable key such as <c>WEB</c> (FR-011, FR-012).</summary>
    public string Key { get; private set; } = "";

    public string Name { get; private set; } = "";

    /// <summary>Upper-invariant name; project names are unique ignoring case.</summary>
    public string NormalizedName { get; private set; } = "";

    public string? Description { get; private set; }

    /// <summary>The creator (FR-011); becomes the first Project Admin in Phase 2.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>The next work item number; incremented atomically when a work item is created.</summary>
    public int NextItemNumber { get; private set; } = 1;

    /// <summary>Incremented on every column change; column commands carry the version they saw.</summary>
    public int BoardVersion { get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Incremented when the name or description changes; edits carry the version they saw.</summary>
    public int DetailsVersion { get; private set; } = 1;

    /// <summary>The board columns in position order.</summary>
    public IReadOnlyList<ProjectStatus> Statuses => _statuses.OrderBy(s => s.Position).ToList();

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
