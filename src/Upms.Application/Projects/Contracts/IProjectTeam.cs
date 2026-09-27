namespace Upms.Application.Projects.Contracts;

/// <summary>A project's team, for the Work module: assignee choices, marks for assignees who can no longer work on the
/// project, and which projects a person can see for "My tasks" (Phase 2 research R7).</summary>
public interface IProjectTeam
{
    /// <summary>Every member of the project, with whether they can work on it.</summary>
    Task<IReadOnlyList<TeamMemberInfo>> GetMembersAsync(long projectId, CancellationToken ct);

    /// <summary>True when the person is an active Project Admin or Member of the project (FR-016).</summary>
    Task<bool> CanBeAssignedAsync(long projectId, Guid userId, CancellationToken ct);

    /// <summary>The projects among <paramref name="projectIds"/> that the person can see: those whose team they are
    /// on, or all of them for an active administrator.</summary>
    Task<IReadOnlyList<ProjectRef>> VisibleAmongAsync(Guid userId, IReadOnlyCollection<long> projectIds, CancellationToken ct);
}

/// <param name="CanWork">An active Project Admin or Member (FR-016, FR-024); false for Viewers and deactivated accounts.</param>
public sealed record TeamMemberInfo(Guid UserId, string DisplayName, bool CanWork);

public sealed record ProjectRef(long Id, string Key, string Name);
