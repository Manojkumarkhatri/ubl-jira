namespace Upms.Domain.Work;

/// <summary>What a history entry records (data-model.md, "WorkItemChange").</summary>
public enum WorkItemField
{
    Created,
    Title,
    Description,
    Priority,
    Status,
    Rank,
    SubtaskAdded,
    CommentAdded,
    CommentEdited,
    CommentDeleted,
    Deleted,
    Restored,
    Assignee,
    StartDate,
    DueDate,

    /// <summary>Story, Task or Bug (Phase 3 FR-011).</summary>
    Type,

    /// <summary>The epic or stage, as "KEY Title" (Phase 3 FR-015).</summary>
    Container,

    /// <summary>The estimate, as "3" or "2.5" (Phase 3 FR-014).</summary>
    Estimate,

    /// <summary>The sprint by name; none is the backlog (Phase 3 FR-030).</summary>
    Sprint,

    /// <summary>A gate's approvers, approvals needed or requester exclusion changed, on its stage (Phase 3 FR-040).</summary>
    GateSetup,

    /// <summary>Approval of the stage's gate was requested (request number in the note).</summary>
    GateRequested,

    /// <summary>The request was withdrawn.</summary>
    GateWithdrawn,

    /// <summary>An approver approved or sent back, with the note.</summary>
    GateDecision,

    /// <summary>The gate got the approvals it needs.</summary>
    GateApproved,

    /// <summary>A dependency was added: "waits for KEY Title" or "is waited for by KEY Title" (Phase 3 FR-052).</summary>
    DependencyAdded,

    /// <summary>A dependency was removed, or went with a deleted item.</summary>
    DependencyRemoved,

    /// <summary>A file was attached; the new value is its name (Phase 4 FR-031).</summary>
    AttachmentAdded,

    /// <summary>A file was removed from the item; the old value is its name.</summary>
    AttachmentRemoved,

    /// <summary>A label was added; the new value is its name (Phase 4 FR-048).</summary>
    LabelAdded,

    /// <summary>A label was removed; the old value is its name.</summary>
    LabelRemoved,

    /// <summary>An epic's color, by name (Jira parity pass FR-025).</summary>
    Color,

    /// <summary>Time was logged; the new value is the time, as "2h 30m" (Jira's "Log work").</summary>
    WorkLogged,

    /// <summary>A work log's time was changed, old and new.</summary>
    WorkLogChanged,

    /// <summary>A work log was deleted; the old value is its time.</summary>
    WorkLogDeleted,

    /// <summary>The original estimate, as "3d 4h".</summary>
    OriginalEstimate,

    /// <summary>The remaining estimate, as "1d".</summary>
    RemainingEstimate,

    /// <summary>The item moved to another space: its old and new keys (Jira's "Move").</summary>
    Moved,

    /// <summary>The parent of a sub-task, by key: set, changed or cleared by a move.</summary>
    Parent,

    /// <summary>The item was made as a copy of another; the old value is that item's key (Jira's "Clone").</summary>
    ClonedFrom,

    /// <summary>A copy of the item was made; the new value is the copy's key.</summary>
    ClonedTo,

    /// <summary>A space field's value changed; the note names the field (Jira's custom fields).</summary>
    CustomField,
}
