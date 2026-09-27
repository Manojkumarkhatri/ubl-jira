namespace Upms.Domain.Identity;

/// <summary>Security events written to the audit log from Day 1 (FR-010).</summary>
public enum AuditEventType
{
    SetupCompleted,
    SignInSucceeded,
    SignInFailed,
    LockedOut,
    PasswordChanged,
    PasswordReset,
    UserCreated,
    UserDeactivated,
    UserReactivated,

    /// <summary>The Administrator role was given or removed; details hold the old and new roles.</summary>
    RoleChanged,
}
