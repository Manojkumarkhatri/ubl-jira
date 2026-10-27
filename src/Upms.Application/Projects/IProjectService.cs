using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Projects (FR-011 to FR-015; Phase 2 FR-002, FR-007, FR-015).</summary>
public interface IProjectService
{
    /// <summary>The projects the caller can see (their own; every project for administrators), sorted by name, a page
    /// at a time (Phase 2 FR-015).</summary>
    Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct);

    /// <summary>A key suggested from the name that is not in use yet (FR-011).</summary>
    Task<string> SuggestKeyAsync(string projectName, CancellationToken ct);

    /// <summary>Creates a project with its template's columns (and, for Stage-gate, its four stages) and the caller as its
    /// first Project Admin; returns its key (Phase 3 FR-001–FR-003).</summary>
    Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template, CancellationToken ct);

    /// <summary>A space from the Blank template: Kanban, with the chosen columns and work types (Jira parity pass FR-016,
    /// FR-017): the column rules, <c>NoStandardType</c>, and the key and name rules of <see cref="CreateAsync"/>.</summary>
    Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct);

    /// <summary>Space admins and administrators turn work types on and off (FR-017): <c>NoStandardType</c>,
    /// <c>WorkTypeInUse</c>, <c>TemplateSwitchNotAllowed</c> for Stage-gate spaces, and <c>Conflict</c> when the details changed
    /// meanwhile.</summary>
    Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, WorkTypeSet workTypes, int expectedDetailsVersion, CancellationToken ct);

    /// <summary>Project Admins and Administrators switch between Kanban and Scrum (Phase 3 FR-005):
    /// <c>TemplateSwitchNotAllowed</c>, <c>SprintsOpen</c>, and <c>Conflict</c> when the details changed meanwhile.</summary>
    Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion,
        CancellationToken ct);

    Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct);

    /// <summary>Project Admins and Administrators (FR-014); <c>Conflict</c> when the details changed meanwhile.</summary>
    Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description,
        int expectedDetailsVersion, CancellationToken ct);
}

/// <param name="MyRole">The caller's role in the project; null when they see it only as an administrator.</param>
/// <param name="AdministratorAccess">True when the caller is an administrator who is not a member.</param>
/// <param name="Template">Shown in the project list (Phase 3 FR-006).</param>
public sealed record ProjectSummary(string Key, string Name, ProjectRole? MyRole, bool AdministratorAccess, int OpenItemCount,
    ProjectTemplate Template = ProjectTemplate.Kanban, bool IsStarred = false);

public sealed record ProjectDetails(
    string Key,
    string Name,
    string? Description,
    string OwnerDisplayName,
    bool CanManage,
    int DetailsVersion,
    DateTimeOffset CreatedAt,
    ProjectTemplate Template = ProjectTemplate.Kanban,
    WorkTypeSet WorkTypes = WorkTypeSet.All,
    IReadOnlyDictionary<WorkTypeSet, int>? TypeCounts = null);

/// <summary>The Blank template's choices (Jira parity pass FR-016).</summary>
public sealed record SpaceSetup(IReadOnlyList<NewColumn> Columns, WorkTypeSet WorkTypes);

public sealed record NewColumn(string Name, StatusCategory Category);
