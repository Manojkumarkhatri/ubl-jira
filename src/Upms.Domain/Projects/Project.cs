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
    public const string TemplateSwitchNotAllowedCode = "TemplateSwitchNotAllowed";
    public const string SprintsOpenCode = "SprintsOpen";
    public const string NoStandardTypeCode = "NoStandardType";
    public const string WorkTypeInUseCode = "WorkTypeInUse";

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

    private Project(string key, string name, string? description, Guid ownerId, ProjectTemplate template, DateTimeOffset createdAt)
    {
        Key = key;
        Name = name;
        NormalizedName = NormalizeName(name);
        Description = description;
        OwnerId = ownerId;
        Template = template;
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

    /// <summary>How the project works (Phase 3 FR-001): Kanban (every project created before Phase 3), Scrum or
    /// Stage-gate. Only Kanban and Scrum can switch into each other (FR-005).</summary>
    public ProjectTemplate Template { get; private set; }

    /// <summary>The work types a Kanban, Scrum or blank space has turned on; all four unless changed (Jira parity pass
    /// FR-017). Stage-gate spaces ignore it.</summary>
    public WorkTypeSet WorkTypes { get; private set; } = WorkTypeSet.All;

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

    /// <summary>The views shown as the space's tabs, in order, as Jira's "Add to navigation" keeps them (for example
    /// "summary,board,list,calendar"); null shows the template's defaults.</summary>
    public string? NavigationViews { get; private set; }

    public const int NavigationViewsMaxLength = 200;

    /// <summary>The portfolio the space belongs to, which gives its leads and its department's Heads access; null for none.</summary>
    public long? PortfolioId { get; private set; }

    /// <summary>In the trash, as Jira's deleted spaces: hidden everywhere until an Admin restores it (owner, 2026-10-26).
    /// Nothing is erased, so its history stays.</summary>
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public void MoveToTrash(Guid byUserId, DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
        DeletedById = byUserId;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedById = null;
        UpdatedAt = now;
    }

    public void MoveToPortfolio(long? portfolioId, DateTimeOffset now)
    {
        PortfolioId = portfolioId;
        UpdatedAt = now;
    }

    /// <summary>Sets the space's tabs; the caller checks the names.</summary>
    public void SetNavigationViews(string? views, DateTimeOffset now)
    {
        NavigationViews = views;
        UpdatedAt = now;
    }

    /// <summary>The board columns in position order.</summary>
    public IReadOnlyList<ProjectStatus> Statuses => _statuses.OrderBy(s => s.Position).ToList();

    /// <summary>The team (Phase 2 FR-001); loaded only by the operations that need it.</summary>
    public IReadOnlyList<ProjectMember> Members => _members;

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    /// <summary>A new project with its template's columns and its creator as the first Project Admin (FR-011, FR-016,
    /// Phase 2 FR-007, Phase 3 FR-002).</summary>
    public static DomainResult<Project> Create(string name, string key, string? description, Guid ownerId, DateTimeOffset now,
        ProjectTemplate template = ProjectTemplate.Kanban)
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

        var project = new Project(normalizedKey, name.Trim(), NormalizeDescription(description), ownerId, template, now);
        var columns = StartingColumns(template);
        for (var i = 0; i < columns.Length; i++)
        {
            project._statuses.Add(new ProjectStatus(columns[i].Name, columns[i].Category, i));
        }

        project._members.Add(new ProjectMember(ownerId, ProjectRole.ProjectAdmin, ownerId, now));
        return project;
    }

    /// <summary>A space from the Blank template: a Kanban space with the columns, in order, and the work types its creator
    /// chose (Jira parity pass FR-016, FR-017; research R13).</summary>
    public static DomainResult<Project> Create(string name, string key, string? description, Guid ownerId, DateTimeOffset now,
        IReadOnlyList<(string Name, StatusCategory Category)> columns, WorkTypeSet workTypes)
    {
        if (CheckWorkTypes(workTypes) is { } noStandardType)
        {
            return noStandardType;
        }

        var created = Create(name, key, description, ownerId, now, ProjectTemplate.Kanban);
        if (!created.IsSuccess)
        {
            return created;
        }

        var project = created.Value!;
        project._statuses.Clear();
        if (columns.Count > MaxColumns)
        {
            return DomainError.Rule(TooManyColumnsCode, $"A board can have at most {MaxColumns} columns.");
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (project.ValidateColumnName(columns[i].Name, null) is { } invalid)
            {
                return invalid.Code == DomainError.ValidationCode ? DomainError.Invalid($"Columns[{i}].Name", invalid.Message) : invalid;
            }

            project._statuses.Add(new ProjectStatus(columns[i].Name.Trim(), columns[i].Category, i));
        }

        if (!project._statuses.Exists(s => s.Category == StatusCategory.ToDo))
        {
            return DomainError.Rule(LastToDoColumnCode, "A board needs at least one \"to do\" column.");
        }

        if (!project._statuses.Exists(s => s.Category == StatusCategory.Done))
        {
            return DomainError.Rule(LastDoneColumnCode, "A board needs at least one \"done\" column.");
        }

        project.WorkTypes = workTypes;
        return project;
    }

    /// <summary>Turns work types on and off in a Kanban, Scrum or blank space (Jira parity pass FR-017). A type that live or
    /// deleted items use stays on; <paramref name="itemsByType"/> counts them, from the Work module.</summary>
    public DomainError? SetWorkTypes(WorkTypeSet workTypes, IReadOnlyDictionary<WorkTypeSet, int> itemsByType, DateTimeOffset now)
    {
        if (Template == ProjectTemplate.StageGate)
        {
            return DomainError.Rule(TemplateSwitchNotAllowedCode, "Stage-gate spaces have fixed work types.");
        }

        if (CheckWorkTypes(workTypes) is { } noStandardType)
        {
            return noStandardType;
        }

        foreach (var (type, count) in itemsByType.Where(t => t.Value > 0 && WorkTypes.HasFlag(t.Key) && !workTypes.HasFlag(t.Key)))
        {
            return DomainError.Rule(WorkTypeInUseCode,
                $"{PluralName(type)} are still used by {count} {(count == 1 ? "item" : "items")} in this space.");
        }

        if (workTypes != WorkTypes)
        {
            WorkTypes = workTypes;
            DetailsVersion++;
            UpdatedAt = now;
        }

        return null;
    }

    private static DomainError? CheckWorkTypes(WorkTypeSet workTypes) =>
        (workTypes & (WorkTypeSet.Story | WorkTypeSet.Task | WorkTypeSet.Bug)) == WorkTypeSet.None
            ? DomainError.Rule(NoStandardTypeCode, "Choose at least one of Story, Task or Bug.")
            : null;

    private static string PluralName(WorkTypeSet type) => type switch
    {
        WorkTypeSet.Epic => "Epics",
        WorkTypeSet.Story => "Stories",
        WorkTypeSet.Task => "Tasks",
        _ => "Bugs",
    };

    /// <summary>Switches between Kanban and Scrum, keeping the columns (Phase 3 FR-005). A Scrum project with an active
    /// or planned sprint cannot become Kanban (the Work module knows, hence <paramref name="hasOpenSprints"/>); no
    /// project switches to or from Stage-gate, because its work item types differ.</summary>
    public DomainError? SwitchTemplate(ProjectTemplate target, bool hasOpenSprints, DateTimeOffset now)
    {
        if (target == Template)
        {
            return null;
        }

        if (target == ProjectTemplate.StageGate || Template == ProjectTemplate.StageGate)
        {
            return DomainError.Rule(TemplateSwitchNotAllowedCode,
                "A space can switch only between Kanban and Scrum; Stage-gate spaces use other work item types.");
        }

        if (Template == ProjectTemplate.Scrum && hasOpenSprints)
        {
            return DomainError.Rule(SprintsOpenCode,
                "Complete or delete the space's active and planned sprints before switching it to Kanban.");
        }

        Template = target;
        DetailsVersion++;
        UpdatedAt = now;
        return null;
    }

    // The columns each template starts with (data-model.md, "Template rules").
    public static (string Name, StatusCategory Category)[] StartingColumns(ProjectTemplate template) => template switch
    {
        ProjectTemplate.Scrum =>
        [
            ("To Do", StatusCategory.ToDo),
            ("In Progress", StatusCategory.InProgress),
            ("In Review", StatusCategory.InProgress),
            ("Done", StatusCategory.Done),
        ],
        ProjectTemplate.StageGate =>
        [
            ("Not Started", StatusCategory.ToDo),
            ("In Progress", StatusCategory.InProgress),
            ("On Hold", StatusCategory.InProgress),
            ("Completed", StatusCategory.Done),
        ],
        _ =>
        [
            ("To Do", StatusCategory.ToDo),
            ("In Progress", StatusCategory.InProgress),
            ("Done", StatusCategory.Done),
        ],
    };

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
                $"This person is already a member of the space ({existing.Role.DisplayName().ToLowerInvariant()}).");
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

    /// <summary>Makes the access given by the space's portfolio or department match <paramref name="granted"/> (its leads
    /// and Heads, each with the role their ticks give): adds those missing, removes grants no longer given, and leaves
    /// people added by hand as they are. True when anything changed.</summary>
    public bool SyncPortfolioAccess(IReadOnlyDictionary<Guid, ProjectRole> granted, DateTimeOffset now)
    {
        var changed = false;
        foreach (var stale in _members.Where(m => m.FromPortfolio && !granted.ContainsKey(m.UserId)).ToList())
        {
            _members.Remove(stale);
            changed = true;
        }

        foreach (var (userId, role) in granted)
        {
            if (_members.FirstOrDefault(m => m.UserId == userId) is { } existing)
            {
                // A grant follows the person's ticks; someone added by hand keeps their role unless the grant gives more.
                if (existing.FromPortfolio ? existing.Role != role : Rank(role) > Rank(existing.Role))
                {
                    existing.ChangeRole(role);
                    changed = true;
                }

                continue;
            }

            var member = new ProjectMember(userId, role, null, now);
            member.SetFromPortfolio(true);
            _members.Add(member);
            changed = true;
        }

        if (changed)
        {
            TeamChanged(now);
        }

        return changed;
    }

    private static int Rank(ProjectRole role) => role switch
    {
        ProjectRole.ProjectAdmin => 2,
        ProjectRole.Member => 1,
        _ => 0,
    };

    /// <summary>Sets a member's role from their ticks after their type or ticks changed. Unlike
    /// <see cref="ChangeMemberRole"/>, it may leave the space without a Space admin: an Admin decided the change.</summary>
    public bool SetRoleFromRights(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (_members.FirstOrDefault(m => m.UserId == userId) is not { } member || member.Role == role || member.FromPortfolio)
        {
            return false;
        }

        member.ChangeRole(role);
        TeamChanged(now);
        return true;
    }

    // Only a change to an active Project Admin can lower their number; with none left, administrators can still repair
    // the project by changing inactive ones and appointing someone.
    private bool WouldLeaveNoActiveProjectAdmin(ProjectMember member, IReadOnlySet<Guid> activeUserIds) =>
        member.Role == ProjectRole.ProjectAdmin
        && activeUserIds.Contains(member.UserId)
        && !_members.Exists(m => m != member && m.Role == ProjectRole.ProjectAdmin && activeUserIds.Contains(m.UserId));

    private static DomainError LastProjectAdmin() =>
        DomainError.Rule(LastProjectAdminCode,
            "This is the last active person who manages the space. Give someone else that access first.");

    private void EnsureOwnMember(ProjectMember member)
    {
        if (!_members.Contains(member))
        {
            throw new InvalidOperationException("The member does not belong to this space.");
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
            0 => DomainError.Invalid("Name", "Enter a space name."),
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
