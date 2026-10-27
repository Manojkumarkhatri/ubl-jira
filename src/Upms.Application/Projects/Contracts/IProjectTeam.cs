using Upms.Application.Common.Results;

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

    /// <summary>Adds an active person who is not on the team as a Viewer, in the caller's transaction, with a
    /// <c>MemberAdded</c> audit event (Phase 3 research R16): a gate approver from outside the project. False when they are
    /// already a member; <c>AccountDeactivated</c> for an inactive account.</summary>
    Task<Result<bool>> AddViewerAsync(long projectId, Guid userId, CancellationToken ct);

    /// <summary>Active members in any role whose display name or user name contains <paramref name="term"/>, by display
    /// name, at most <paramref name="take"/>; with no term, the first of them (Phase 4 FR-008, research R6).</summary>
    Task<IReadOnlyList<TeamMemberInfo>> FindMembersAsync(long projectId, string term, int take, CancellationToken ct);

    /// <summary>Which of these people can see the project now: its active members, and active administrators (Phase 4
    /// FR-014, research R8).</summary>
    Task<IReadOnlySet<Guid>> WhoCanSeeAsync(long projectId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

/// <param name="CanWork">An active Project Admin or Member (FR-016, FR-024); false for Viewers and deactivated accounts.</param>
/// <param name="IsActive">An active account: as a member of any role, they can still decide on a gate (Phase 3 FR-036).</param>
/// <param name="UserName">Shown beside the name in mention suggestions (Phase 4 FR-008).</param>
public sealed record TeamMemberInfo(Guid UserId, string DisplayName, bool CanWork, bool IsActive = true, string UserName = "");

public sealed record ProjectRef(long Id, string Key, string Name);
