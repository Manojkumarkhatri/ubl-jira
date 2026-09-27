using Upms.Application.Common.Results;
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

    /// <summary>True for administrators, the project's Project Admins, and the item's creator while they can still
    /// contribute.</summary>
    Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct);
}

/// <summary>The project and the caller's standing in it.</summary>
/// <param name="Role">The caller's project role; null for an administrator who is not a member.</param>
public sealed record ProjectAccessInfo(long ProjectId, string Key, Guid UserId, ProjectRole? Role, bool CanContribute,
    bool CanManage, bool IsAdministrator);
