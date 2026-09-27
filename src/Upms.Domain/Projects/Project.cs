using System.Text.RegularExpressions;
using Upms.Domain.Common;

namespace Upms.Domain.Projects;

/// <summary>A body of work with its own board (data-model.md, "Project").</summary>
public sealed partial class Project
{
    public const string InvalidKeyCode = "InvalidProjectKey";

    public const int KeyMinLength = 2;
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectStatus> _statuses = [];

    private Project()
    {
    }

    private Project(string key, string name, string? description, Guid ownerId, DateTimeOffset createdAt)
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

    /// <summary>A new project owned by its creator, with the default Kanban columns (FR-011, FR-016).</summary>
    public static DomainResult<Project> Create(string name, string key, string? description, Guid ownerId, DateTimeOffset now)
    {
        var normalizedKey = (key ?? "").Trim().ToUpperInvariant();
        if (!KeyPattern().IsMatch(normalizedKey))
        {
            return new DomainError(InvalidKeyCode,
                "The key must be 2–10 capital letters or digits and start with a letter, for example WEB.", "Key");
        }

        if (ValidateName(name) is { } nameError)
        {
            return nameError;
        }

        if (ValidateDescription(description) is { } descriptionError)
        {
            return descriptionError;
        }

        var project = new Project(normalizedKey, name.Trim(), NormalizeDescription(description), ownerId, now);
        project._statuses.Add(new ProjectStatus("To Do", StatusCategory.ToDo, 0));
        project._statuses.Add(new ProjectStatus("In Progress", StatusCategory.InProgress, 1));
        project._statuses.Add(new ProjectStatus("Done", StatusCategory.Done, 2));
        return project;
    }

    /// <summary>Changes the name and description; the key never changes (FR-012, FR-014).</summary>
    public DomainError? UpdateDetails(string name, string? description, DateTimeOffset now)
    {
        if ((ValidateName(name) ?? ValidateDescription(description)) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        NormalizedName = NormalizeName(name);
        Description = NormalizeDescription(description);
        DetailsVersion++;
        UpdatedAt = now;
        return null;
    }

    public static bool IsValidKey(string key) => KeyPattern().IsMatch(key);

    private static DomainError? ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed.Length switch
        {
            0 => DomainError.Invalid("Name", "Enter a project name."),
            > NameMaxLength => DomainError.Invalid("Name", $"The name can have at most {NameMaxLength} characters."),
            _ => null,
        };
    }

    private static DomainError? ValidateDescription(string? description) =>
        NormalizeDescription(description) is { Length: > DescriptionMaxLength }
            ? DomainError.Invalid("Description", $"The description can have at most {DescriptionMaxLength} characters.")
            : null;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex KeyPattern();
}
