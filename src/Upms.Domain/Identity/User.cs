using Microsoft.AspNetCore.Identity;

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
