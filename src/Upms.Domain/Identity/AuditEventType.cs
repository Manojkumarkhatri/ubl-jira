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

    /// <summary>A person joined a project's team (Phase 2 FR-012); the target is the project key.</summary>
    MemberAdded,

    /// <summary>A person left a project's team.</summary>
    MemberRemoved,

    /// <summary>A member's project role changed; details hold the old and new roles.</summary>
    MemberRoleChanged,
}
