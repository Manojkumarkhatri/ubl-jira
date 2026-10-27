using Microsoft.AspNetCore.Identity;
using Upms.Domain.Common;

namespace Upms.Domain.Identity;

/// <summary>A person with an account (data-model.md, "User"). Extends the ASP.NET Core Identity user,
/// whose store requires public setters on the Identity properties.</summary>
public class User : IdentityUser<Guid>
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 64;
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 100;
    public const int TimeZoneIdMaxLength = 64;

    /// <summary>Longer passwords are refused (OWASP ASVS 2.1.2); at least 64 characters are always allowed.</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Characters allowed in user names: letters, digits, ".", "-" and "_".</summary>
    public const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-_";

    /// <summary>Required, 1–100 characters (FR-007).</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>An IANA time zone ID; null means the organization default (FR-007, FR-043).</summary>
    public string? TimeZoneId { get; set; }

    public OrganizationRole OrganizationRole { get; set; } = OrganizationRole.User;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeactivatedAt { get; set; }

    /// <summary>Set after creation or a password reset; the user must choose a new password (FR-003).</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSignInAt { get; set; }

    /// <summary>Which notifications are also emailed (Phase 4 FR-049, FR-051).</summary>
    public EmailChoice EmailNotifications { get; set; } = EmailChoice.AimedAtMe;

    /// <summary>Whether the person keeps the sidebar collapsed (Jira parity pass FR-021).</summary>
    public bool SidebarCollapsed { get; set; }

    /// <summary>Which of the five kinds of user the person is; an Admin is also an <see cref="OrganizationRole.Administrator"/>.</summary>
    public UserType UserType { get; set; } = UserType.Member;

    /// <summary>The department the person belongs to; a Head sees all of it. Null for none.</summary>
    public long? DepartmentId { get; set; }

    /// <summary>Ticks given to this person beyond their type's.</summary>
    public Rights RightsGranted { get; set; }

    /// <summary>Ticks of their type taken away from this person.</summary>
    public Rights RightsDenied { get; set; }

    public bool IsAdministrator => OrganizationRole == OrganizationRole.Administrator;

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        DeactivatedAt = now;
    }

    public void Reactivate()
    {
        IsActive = true;
        DeactivatedAt = null;
    }
}
