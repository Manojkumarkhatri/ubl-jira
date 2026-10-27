namespace Upms.Domain.Common;

/// <summary>Why someone is notified (Phase 4 FR-015, FR-016, data model). Shared by the Work module, which raises the
/// events, and the Notifications module, which keeps them, like <see cref="ProjectTemplate"/>.</summary>
public enum NotificationKind
{
    /// <summary>Someone else assigned the item to the recipient.</summary>
    Assigned,

    /// <summary>Someone else mentioned the recipient for the first time in a description or comment.</summary>
    Mentioned,

    /// <summary>Someone else commented on an item the recipient watches.</summary>
    Commented,

    /// <summary>Someone else changed the status of an item the recipient watches; the detail is the new status.</summary>
    StatusChanged,

    /// <summary>Approval was requested of a gate the recipient approves and may decide on; the item is the stage.</summary>
    ApprovalRequested,

    /// <summary>The recipient's gate request was approved or sent back; the detail is the outcome.</summary>
    GateDecided,

    /// <summary>The recipient's upload was blocked by the malware check; the detail is the file name.</summary>
    UploadBlocked,
}
