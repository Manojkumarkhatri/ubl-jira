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

    /// <summary>A project switched between Kanban and Scrum (Phase 3 FR-005); the target is the project key.</summary>
    ProjectTemplateChanged,

    /// <summary>A gate's approvers, the approvals it needs or its requester exclusion changed (Phase 3 FR-040); the
    /// target is the stage key.</summary>
    GateSetupChanged,

    /// <summary>Approval of a gate was requested.</summary>
    GateApprovalRequested,

    /// <summary>A pending gate request was withdrawn.</summary>
    GateRequestWithdrawn,

    /// <summary>An approver approved a gate or sent it back; details hold the decision and its note.</summary>
    GateDecisionRecorded,

    /// <summary>A gate received the approvals it needs and its stage's successor unlocked.</summary>
    GateApproved,

    /// <summary>An uploaded file was found infected and quarantined (Phase 4 FR-025); the target is the item key, the details
    /// hold the file name, its SHA-256 and the signature name.</summary>
    MalwareDetected,

    /// <summary>An administrator deleted an attachment permanently (Phase 4 FR-029); the target is the item key, the details
    /// hold the file name, size and uploader.</summary>
    AttachmentPurged,

    /// <summary>An email could not be delivered within 24 hours (Phase 4 FR-052); the target is the recipient's id, the
    /// details hold the notification kind, the item key and the attempts.</summary>
    EmailGivenUp,

    /// <summary>A space's work types were turned on or off (Jira parity pass FR-017).</summary>
    ProjectWorkTypesChanged,

    /// <summary>A person's type, department or own ticks changed; details hold the before and after.</summary>
    AccessChanged,

    /// <summary>A user type's default ticks changed; the target is the type.</summary>
    TypeRightsChanged,

    /// <summary>A department was added, renamed or deleted; details hold its name.</summary>
    DepartmentChanged,

    /// <summary>A space was moved to the trash (owner, 2026-10-26); the target is its key, the details hold its name.</summary>
    SpaceMovedToTrash,

    /// <summary>An Admin restored a space from the trash; the target is its key.</summary>
    SpaceRestored,
}
