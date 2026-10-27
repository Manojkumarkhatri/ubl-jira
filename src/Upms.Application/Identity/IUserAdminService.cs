using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>Account management (FR-003, FR-004, FR-008). Administrators manage every account; since the five user types
/// (owner's decision of 2026-10-20), a Head with "Add people" or "Set people's type and ticks" lists, adds and sets the
/// people of their own department.</summary>
public interface IUserAdminService
{
    Task<Result<Page<UserSummary>>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct);

    /// <summary>The list with the User management page's filters: a type and active or deactivated.</summary>
    Task<Result<Page<UserSummary>>> FindUsersAsync(UserQuery query, PageRequest page, CancellationToken ct) =>
        ListUsersAsync(query.Search, page, ct);

    /// <summary>How many people of each type the caller manages (their department outside the Admins), for the filter.</summary>
    Task<IReadOnlyDictionary<UserType, int>> TypeCountsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<UserType, int>>(new Dictionary<UserType, int>());

    /// <summary>Creates an account with a temporary password that is returned only once.</summary>
    Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email, CancellationToken ct);

    /// <summary>Creates an account of a type, in a department; a Head adds only to their own department, and not Admins or
    /// Heads.</summary>
    Task<Result<CreatedUser>> AddPersonAsync(NewPerson person, CancellationToken ct);

    /// <summary>Issues a new temporary password.</summary>
    Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct);

    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct);

    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct);

    /// <summary>Gives or removes the Administrator role (FR-008). Only an active account can become an administrator,
    /// and the last active administrator keeps the role (<c>AccountDeactivated</c>, <c>LastAdministrator</c>).</summary>
    Task<Result> ChangeRoleAsync(Guid userId, OrganizationRole role, CancellationToken ct);

    /// <summary>Sets a person's type, department and own ticks, and updates their access to spaces. Making someone an
    /// Admin makes them an administrator, with the rules of <see cref="ChangeRoleAsync"/>.</summary>
    Task<Result> UpdateAccessAsync(Guid userId, AccessChange change, CancellationToken ct);

    /// <summary>What the caller may do on the people page.</summary>
    Task<PeopleRights> RightsAsync(CancellationToken ct);
}

/// <param name="Type">The badge under the person's name.</param>
/// <param name="Rights">Their ticks now: their type's, plus <paramref name="Granted"/>, less <paramref name="Denied"/>.</param>
public sealed record UserSummary(
    Guid Id,
    string UserName,
    string DisplayName,
    string Email,
    OrganizationRole Role,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignInAt)
{
    public UserType Type { get; init; } = Role == OrganizationRole.Administrator ? UserType.Admin : UserType.Member;

    public long? DepartmentId { get; init; }

    public string? Department { get; init; }

    public Rights Granted { get; init; }

    public Rights Denied { get; init; }

    public Rights Rights { get; init; }
}

public sealed record CreatedUser(Guid Id, string UserName, string TemporaryPassword);

/// <param name="Active">True for active accounts, false for deactivated ones, null for both.</param>
public sealed record UserQuery(string? Search, UserType? Type = null, bool? Active = null);

public sealed record NewPerson(string UserName, string DisplayName, string Email, UserType Type, long? DepartmentId);

/// <param name="Granted">Ticks beyond the type's.</param>
/// <param name="Denied">Ticks of the type taken away.</param>
public sealed record AccessChange(UserType Type, long? DepartmentId, Rights Granted, Rights Denied);

/// <param name="IsAdministrator">Manages every account and type.</param>
/// <param name="DepartmentId">The caller's own department, the only one they manage when not an Admin.</param>
/// <param name="Types">The types the caller may give: below their own, with the tick to add that type.</param>
/// <param name="Rights">The ticks the caller may give or take: outside the Admins, only their own.</param>
public sealed record PeopleRights(bool CanList, bool CanAdd, bool CanSetAccess, bool IsAdministrator, long? DepartmentId,
    IReadOnlyList<UserType> Types, Rights Rights)
{
    /// <summary>The caller's own type: outside the Admins, they manage only people below it.</summary>
    public UserType Type { get; init; } = UserType.Admin;

    /// <summary>Resets passwords, deactivates and reactivates the people they manage.</summary>
    public bool CanManageAccounts { get; init; }

    /// <summary>Whether the caller may change this person: an Admin anyone, others the people below their own type in their
    /// own department (owner, 2026-10-26).</summary>
    public bool Manages(UserType type, long? departmentId) =>
        IsAdministrator || (DepartmentId is not null && departmentId == DepartmentId && type.Rank() < Type.Rank());
}
