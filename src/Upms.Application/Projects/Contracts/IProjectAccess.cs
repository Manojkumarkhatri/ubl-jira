using Upms.Application.Common.Results;

namespace Upms.Application.Projects.Contracts;

/// <summary>Rights a caller can hold in a project (research R7).</summary>
public enum ProjectRight
{
    /// <summary>See the project, its board and tasks.</summary>
    View,

    /// <summary>Create, edit, move and comment on tasks.</summary>
    Contribute,

    /// <summary>Delete a work item the caller created (checked per item with
    /// <see cref="IProjectAccess.CanDeleteWorkItemAsync"/>).</summary>
    DeleteOwnWorkItem,

    /// <summary>Edit project details and columns, and delete any work item.</summary>
    Manage,

    /// <summary>List and restore deleted work items.</summary>
    Restore,
}

/// <summary>The single authorization point for projects (research R7). Phase 1 applies the open
/// workspace rules; Phase 2 replaces the implementation with project membership, without caller changes.
/// Every application-service method calls it before any other work.</summary>
public interface IProjectAccess
{
    Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct);

    Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectRight right, CancellationToken ct);

    /// <summary>True when the caller created the item, owns the project or is an Administrator.</summary>
    Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct);
}

/// <summary>The project and the caller's standing in it.</summary>
public sealed record ProjectAccessInfo(long ProjectId, string Key, Guid UserId, bool CanManage, bool IsAdministrator);
