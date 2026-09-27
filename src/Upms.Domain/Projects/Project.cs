using System.Text.RegularExpressions;
using Upms.Domain.Common;

namespace Upms.Domain.Projects;

/// <summary>A body of work with its own board (data-model.md, "Project").</summary>
public sealed partial class Project
{
    public const string InvalidKeyCode = "InvalidProjectKey";
    public const string DuplicateColumnNameCode = "DuplicateColumnName";
    public const string TooManyColumnsCode = "TooManyColumns";
    public const string LastToDoColumnCode = "LastToDoColumn";
    public const string LastDoneColumnCode = "LastDoneColumn";
    public const string ColumnNotEmptyCode = "ColumnNotEmpty";
    public const string DestinationRequiredCode = "DestinationRequired";
    public const string DuplicateMemberCode = "DuplicateMember";
    public const string LastProjectAdminCode = "LastProjectAdmin";

    /// <summary>A board holds at most this many columns (FR-034).</summary>
    public const int MaxColumns = 10;

    public const int KeyMinLength = 2;
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectStatus> _statuses = [];
    private readonly List<ProjectMember> _members = [];

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

    /// <summary>The creator (FR-011), who becomes the first Project Admin; since Phase 2 it grants no rights itself.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>The next work item number; incremented atomically when a work item is created.</summary>
    public int NextItemNumber { get; private set; } = 1;

    /// <summary>Incremented on every column change; column commands carry the version they saw.</summary>
    public int BoardVersion { get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Incremented when the name or description changes; edits carry the version they saw.</summary>
    public int DetailsVersion { get; private set; } = 1;

    /// <summary>Incremented on every membership change; team commands carry the version they saw (Phase 2 FR-013).</summary>
    public int MembersVersion { get; private set; } = 1;

    /// <summary>The board columns in position order.</summary>
    public IReadOnlyList<ProjectStatus> Statuses => _statuses.OrderBy(s => s.Position).ToList();

    /// <summary>The team (Phase 2 FR-001); loaded only by the operations that need it.</summary>
    public IReadOnlyList<ProjectMember> Members => _members;

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    /// <summary>A new project with the default Kanban columns and its creator as the first Project Admin (FR-011,
    /// FR-016, Phase 2 FR-007).</summary>
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
        project._members.Add(new ProjectMember(ownerId, ProjectRole.ProjectAdmin, ownerId, now));
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

    // ----- Team (Phase 2 FR-008 to FR-013). Every change increments MembersVersion. -----

    /// <summary>Adds a person with a role; the caller checks that their account is active.</summary>
    public DomainResult<ProjectMember> AddMember(Guid userId, ProjectRole role, Guid addedById, DateTimeOffset now)
    {
        if (_members.FirstOrDefault(m => m.UserId == userId) is { } existing)
        {
            return DomainError.Rule(DuplicateMemberCode,
                $"This person is already a member of the project, as {existing.Role.DisplayName()}.");
        }

        var member = new ProjectMember(userId, role, addedById, now);
        _members.Add(member);
        TeamChanged(now);
        return member;
    }

    /// <param name="activeUserIds">The members whose accounts are active (the Identity module knows).</param>
    public DomainError? ChangeMemberRole(ProjectMember member, ProjectRole role, IReadOnlySet<Guid> activeUserIds, DateTimeOffset now)
    {
        EnsureOwnMember(member);
        if (member.Role == role)
        {
            return null;
        }

        if (WouldLeaveNoActiveProjectAdmin(member, activeUserIds))
        {
            return LastProjectAdmin();
        }

        member.ChangeRole(role);
        TeamChanged(now);
        return null;
    }

    /// <param name="activeUserIds">The members whose accounts are active (the Identity module knows).</param>
    public DomainError? RemoveMember(ProjectMember member, IReadOnlySet<Guid> activeUserIds, DateTimeOffset now)
    {
        EnsureOwnMember(member);
        if (WouldLeaveNoActiveProjectAdmin(member, activeUserIds))
        {
            return LastProjectAdmin();
        }

        _members.Remove(member);
        TeamChanged(now);
        return null;
    }

    // Only a change to an active Project Admin can lower their number; with none left, administrators can still repair
    // the project by changing inactive ones and appointing someone.
    private bool WouldLeaveNoActiveProjectAdmin(ProjectMember member, IReadOnlySet<Guid> activeUserIds) =>
        member.Role == ProjectRole.ProjectAdmin
        && activeUserIds.Contains(member.UserId)
        && !_members.Exists(m => m != member && m.Role == ProjectRole.ProjectAdmin && activeUserIds.Contains(m.UserId));

    private static DomainError LastProjectAdmin() =>
        DomainError.Rule(LastProjectAdminCode,
            "This is the project's last active Project Admin. Make someone else a Project Admin first.");

    private void EnsureOwnMember(ProjectMember member)
    {
        if (!_members.Contains(member))
        {
            throw new InvalidOperationException("The member does not belong to this project.");
        }
    }

    private void TeamChanged(DateTimeOffset now)
    {
        MembersVersion++;
        UpdatedAt = now;
    }

    // ----- Board columns (FR-034 to FR-039). Every change increments BoardVersion (FR-041). -----

    /// <summary>Adds a column at <paramref name="position"/> (0-based; beyond the ends means first or last).</summary>
    public DomainResult<ProjectStatus> AddColumn(string name, StatusCategory category, int position, DateTimeOffset now)
    {
        if (ValidateColumnName(name, null) is { } error)
        {
            return error;
        }

        if (_statuses.Count >= MaxColumns)
        {
            return DomainError.Rule(TooManyColumnsCode, $"A board can have at most {MaxColumns} columns.");
        }

        var ordered = Statuses.ToList();
        var column = new ProjectStatus(name.Trim(), category, 0);
        ordered.Insert(Math.Clamp(position, 0, ordered.Count), column);
        _statuses.Add(column);
        Renumber(ordered);
        BoardChanged(now);
        return column;
    }

    public DomainError? RenameColumn(ProjectStatus column, string name, DateTimeOffset now)
    {
        EnsureOwnColumn(column);
        if (ValidateColumnName(name, column) is { } error)
        {
            return error;
        }

        if (string.Equals(column.Name, name.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        column.Rename(name.Trim());
        BoardChanged(now);
        return null;
    }

    /// <summary>Moves a column to <paramref name="position"/>; the others close up (FR-035).</summary>
    public DomainError? MoveColumn(ProjectStatus column, int position, DateTimeOffset now)
    {
        EnsureOwnColumn(column);
        var ordered = Statuses.ToList();
        var target = Math.Clamp(position, 0, ordered.Count - 1);
        if (target == column.Position)
        {
            return null;
        }

        ordered.Remove(column);
        ordered.Insert(target, column);
        Renumber(ordered);
        BoardChanged(now);
        return null;
    }

    /// <summary>Sets a work-in-progress limit of 1–99, or removes it with null (FR-036).</summary>
    public DomainError? SetWipLimit(ProjectStatus column, int? limit, DateTimeOffset now)
    {
        EnsureOwnColumn(column);
        if (limit is < ProjectStatus.MinWipLimit or > ProjectStatus.MaxWipLimit)
        {
            return DomainError.Invalid("WipLimit",
                $"A limit must be between {ProjectStatus.MinWipLimit} and {ProjectStatus.MaxWipLimit}, or empty for no limit.");
        }

        if (column.WipLimit == limit)
        {
            return null;
        }

        column.LimitTo(limit);
        BoardChanged(now);
        return null;
    }

    /// <summary>Changes a column's type while no work item, deleted ones included, has it (FR-038, FR-039).</summary>
    public DomainError? ChangeColumnCategory(ProjectStatus column, StatusCategory category, int itemsInColumn, DateTimeOffset now)
    {
        EnsureOwnColumn(column);
        if (column.Category == category)
        {
            return null;
        }

        if (LastOfItsKind(column) is { } lastError)
        {
            return lastError;
        }

        if (itemsInColumn > 0)
        {
            return DomainError.Rule(ColumnNotEmptyCode,
                $"Only an empty column can change type. Move the work items out of {column.Name} first.");
        }

        column.ChangeCategory(category);
        BoardChanged(now);
        return null;
    }

    /// <summary>Removes a column. Work items in it, deleted ones included, must first move to
    /// <paramref name="destination"/> (FR-037); the board keeps a "to do" and a "done" column (FR-038).</summary>
    public DomainError? RemoveColumn(ProjectStatus column, ProjectStatus? destination, int itemsInColumn, DateTimeOffset now)
    {
        EnsureOwnColumn(column);
        if (LastOfItsKind(column) is { } lastError)
        {
            return lastError;
        }

        if (destination is not null)
        {
            EnsureOwnColumn(destination);
            if (ReferenceEquals(destination, column))
            {
                return DomainError.Invalid("DestinationColumnId", "Choose another column for its work items.");
            }
        }
        else if (itemsInColumn > 0)
        {
            return DomainError.Rule(DestinationRequiredCode,
                $"{column.Name} holds work items. Choose the column they should move to.");
        }

        _statuses.Remove(column);
        Renumber(Statuses.ToList());
        BoardChanged(now);
        return null;
    }

    private DomainError? ValidateColumnName(string name, ProjectStatus? renaming)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return DomainError.Invalid("Name", "Enter a column name.");
        }

        if (trimmed.Length > ProjectStatus.NameMaxLength)
        {
            return DomainError.Invalid("Name", $"A column name can have at most {ProjectStatus.NameMaxLength} characters.");
        }

        var normalized = NormalizeName(trimmed);
        return _statuses.Exists(s => !ReferenceEquals(s, renaming) && s.NormalizedName == normalized)
            ? DomainError.Rule(DuplicateColumnNameCode, $"This board already has a column named {trimmed}.")
            : null;
    }

    private DomainError? LastOfItsKind(ProjectStatus column) => column.Category switch
    {
        StatusCategory.ToDo when _statuses.Count(s => s.Category == StatusCategory.ToDo) == 1 =>
            DomainError.Rule(LastToDoColumnCode, $"{column.Name} is the board's only \"to do\" column, and every board needs one."),
        StatusCategory.Done when _statuses.Count(s => s.Category == StatusCategory.Done) == 1 =>
            DomainError.Rule(LastDoneColumnCode, $"{column.Name} is the board's only \"done\" column, and every board needs one."),
        _ => null,
    };

    private void EnsureOwnColumn(ProjectStatus column)
    {
        if (!_statuses.Contains(column))
        {
            throw new ArgumentException("The column is not on this project's board.", nameof(column));
        }
    }

    private static void Renumber(List<ProjectStatus> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].MoveTo(i);
        }
    }

    private void BoardChanged(DateTimeOffset now)
    {
        BoardVersion++;
        UpdatedAt = now;
    }

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
