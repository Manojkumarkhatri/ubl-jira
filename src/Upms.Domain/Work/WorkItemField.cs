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
}
