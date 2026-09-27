using Upms.Application.Common.Results;

namespace Upms.Application.Identity;

/// <summary>The signed-in user's own account (FR-007).</summary>
public interface IAccountService
{
    Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct);

    /// <summary>Display name 1–100 characters; time zone an IANA ID, or null for the organization default.</summary>
    Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct);

    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct);
}

/// <param name="TimeZoneId">The user's own choice, or null for the organization default.</param>
/// <param name="EffectiveTimeZoneId">The time zone used to show times (FR-043).</param>
public sealed record MyProfile(
    Guid Id,
    string UserName,
    string DisplayName,
    string Email,
    string? TimeZoneId,
    string EffectiveTimeZoneId,
    bool IsAdministrator);
