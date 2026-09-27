using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>Minimal account management for administrators (FR-003, FR-004).</summary>
public interface IUserAdminService
{
    Task<Result<Page<UserSummary>>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct);

    /// <summary>Creates an account with a temporary password that is returned only once.</summary>
    Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email, CancellationToken ct);

    /// <summary>Issues a new temporary password.</summary>
    Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct);

    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct);

    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct);
}

public sealed record UserSummary(
    Guid Id,
    string UserName,
    string DisplayName,
    string Email,
    OrganizationRole Role,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignInAt);

public sealed record CreatedUser(Guid Id, string UserName, string TemporaryPassword);
