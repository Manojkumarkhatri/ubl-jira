using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects.Contracts;

/// <summary>Rights a caller can hold in a project (research R7).</summary>
public enum ProjectRight
{
    /// <summary>See the project, its board and tasks.</summary>
    View,

    /// <summary>Create, edit, move, assign, schedule and comment on tasks.</summary>
    Contribute,

    /// <summary>Delete a work item the caller created (checked per item with
    /// <see cref="IProjectAccess.CanDeleteWorkItemAsync"/>).</summary>
    DeleteOwnWorkItem,

    /// <summary>Edit project details, columns and the team, and delete any work item.</summary>
    Manage,

    /// <summary>List and restore deleted work items.</summary>
    Restore,
}

/// <summary>The single authorization point for projects (research R7). Since Phase 2 it applies project membership
/// (Phase 2 research R2): people who are neither members nor administrators get <c>NotFound</c>, as for a project that
/// does not exist. Every application-service method calls it before any other work.</summary>
public interface IProjectAccess
{
    Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct);

    Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectRight right, CancellationToken ct);

    /// <summary>True for administrators; otherwise, in a space where the caller can edit, with the "Delete any work item"
    /// tick, or "Delete work items they created" for their own (owner, 2026-10-26).</summary>
    Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct);

    /// <summary>Every project the caller can see: their memberships in any role, or null (every project) for an
    /// administrator; none for a deactivated or signed-out caller (Phase 4 research R8, R15).</summary>
    Task<IReadOnlySet<long>?> VisibleProjectIdsAsync(CancellationToken ct);

    /// <summary>The projects the caller is a member of, in any role, administrator or not: filters shared with a project
    /// reach its members (Phase 4 FR-040).</summary>
    Task<IReadOnlySet<long>> MemberProjectIdsAsync(CancellationToken ct);

    /// <summary>The spaces the caller can add work to, by name: those where they are a Space admin or a Member, and every
    /// space for an administrator; none for a deactivated or signed-out caller (Jira parity pass FR-002).</summary>
    Task<IReadOnlyList<ContributableSpace>> ContributableAsync(CancellationToken ct);
}

/// <param name="CanManage">A Space admin or an administrator, who may also create stages (FR-003).</param>
public sealed record ContributableSpace(long ProjectId, string Key, string Name, ProjectTemplate Template, WorkTypeSet WorkTypes,
    bool CanManage);

/// <summary>The project and the caller's standing in it.</summary>
/// <param name="Role">The caller's project role; null for an administrator who is not a member.</param>
/// <param name="Template">How the project works (Phase 3 research R2), so services need no second query.</param>
/// <param name="WorkTypes">The work types the space has turned on (Jira parity pass FR-017; research R14).</param>
public sealed record ProjectAccessInfo(long ProjectId, string Key, Guid UserId, ProjectRole? Role, bool CanContribute,
    bool CanManage, bool IsAdministrator, ProjectTemplate Template = ProjectTemplate.Kanban, WorkTypeSet WorkTypes = WorkTypeSet.All);
