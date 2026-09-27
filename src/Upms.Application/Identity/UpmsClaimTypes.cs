namespace Upms.Application.Identity;

/// <summary>Custom claims written into the authentication cookie at sign-in.</summary>
public static class UpmsClaimTypes
{
    public const string DisplayName = "upms:display_name";

    /// <summary>Present (value "true") while the user must replace a temporary password (FR-003).</summary>
    public const string MustChangePassword = "upms:must_change_password";

    /// <summary>A random ID per sign-in, used to track the session's idle time across tabs (FR-006).</summary>
    public const string SessionId = "upms:sid";
}
