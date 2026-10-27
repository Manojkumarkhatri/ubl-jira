using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Identity;

/// <summary>The signed-in user's own account (FR-007).</summary>
public interface IAccountService
{
    Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct);

    /// <summary>Display name 1–100 characters; time zone an IANA ID, or null for the organization default.</summary>
    Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct);

    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>Which notifications the caller also gets by email (Phase 4 FR-051); only one's own choice.</summary>
    Task<Result<MyProfile>> SetEmailChoiceAsync(EmailChoice choice, CancellationToken ct);

    /// <summary>Keeps the sidebar collapsed or open for the signed-in person (Jira parity pass FR-021).</summary>
    Task<Result<MyProfile>> SetSidebarCollapsedAsync(bool collapsed, CancellationToken ct);
}

/// <param name="TimeZoneId">The user's own choice, or null for the organization default.</param>
/// <param name="EffectiveTimeZoneId">The time zone used to show times (FR-043).</param>
/// <param name="EmailChoice">Which notifications are also emailed (Phase 4 FR-051).</param>
public sealed record MyProfile(
    Guid Id,
    string UserName,
    string DisplayName,
    string Email,
    string? TimeZoneId,
    string EffectiveTimeZoneId,
    bool IsAdministrator,
    EmailChoice EmailChoice = EmailChoice.AimedAtMe,
    bool SidebarCollapsed = false)
{
    /// <summary>The badge under the person's name (owner's decision of 2026-10-20).</summary>
    public UserType Type { get; init; } = IsAdministrator ? UserType.Admin : UserType.Member;
}
