using Upms.Application.Common.Results;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>A project's team (Phase 2 FR-001, FR-008–FR-013): every member sees it; Project Admins and administrators
/// change it. Changes carry the team version they saw (<see cref="TeamView.MembersVersion"/>).</summary>
public interface IProjectMemberService
{
    Task<Result<TeamView>> GetTeamAsync(string projectKey, CancellationToken ct);

    /// <summary>Active accounts matching the term that are not members yet, at most 20 (research R5).</summary>
    Task<Result<IReadOnlyList<PersonOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct);

    /// <summary><c>DuplicateMember</c>, <c>AccountDeactivated</c>; <c>Conflict</c> when the team changed meanwhile.</summary>
    Task<Result<TeamView>> AddAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion, CancellationToken ct);

    /// <summary><c>LastProjectAdmin</c>; <c>Conflict</c> when the team changed meanwhile.</summary>
    Task<Result<TeamView>> ChangeRoleAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion,
        CancellationToken ct);

    /// <summary><c>LastProjectAdmin</c>; <c>Conflict</c> when the team changed meanwhile.</summary>
    Task<Result<TeamView>> RemoveAsync(string projectKey, Guid userId, int expectedMembersVersion, CancellationToken ct);
}

/// <param name="Members">Project Admins first, then by display name.</param>
/// <param name="CanRestoreDeleted">True for administrators, for the project header's "Deleted tasks".</param>
public sealed record TeamView(string ProjectKey, string ProjectName, int MembersVersion, bool CanManage,
    IReadOnlyList<MemberView> Members, bool CanRestoreDeleted = false);

public sealed record MemberView(Guid UserId, string DisplayName, string UserName, ProjectRole Role, bool IsActive, bool IsMe,
    DateTimeOffset AddedAt);

public sealed record PersonOption(Guid UserId, string DisplayName, string UserName);
