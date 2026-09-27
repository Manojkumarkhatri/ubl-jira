using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A task's details drawer (FR-023 to FR-033; Phase 2 FR-016–FR-019, FR-021, FR-023, FR-024).</summary>
public interface IWorkItemService
{
    /// <summary>Details with the first page (50) of sub-tasks, comments and history.</summary>
    Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct);

    /// <summary>Saves one field (FR-026); <c>Conflict</c> with the latest values when someone else changed the
    /// task first (FR-032). Warnings list open sub-tasks when a task becomes done (FR-029).</summary>
    Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit, byte[] expectedVersion, CancellationToken ct);

    /// <summary>A sub-task in the leftmost "to do" column (FR-028, FR-040); returns the parent's details.</summary>
    Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title, CancellationToken ct);

    /// <summary>Moves a sub-task to the leftmost "done" column; returns the parent's details.</summary>
    Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion, CancellationToken ct);

    Task<Result<DeletePreview>> PreviewDeleteAsync(string workItemKey, CancellationToken ct);

    /// <summary>The creator (while they can contribute), a Project Admin or an Administrator (FR-033, Phase 2 FR-004); sub-tasks are deleted with it.</summary>
    Task<Result> DeleteAsync(string workItemKey, CancellationToken ct);

    /// <summary>Administrators only.</summary>
    Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page, CancellationToken ct);

    /// <summary>Administrators only; restores the sub-tasks deleted with it.</summary>
    Task<Result> RestoreAsync(string workItemKey, CancellationToken ct);

    Task<Result<Page<SubtaskView>>> ListSubtasksAsync(string parentKey, PageRequest page, CancellationToken ct);

    /// <summary>The complete history in time order (FR-031).</summary>
    Task<Result<Page<ChangeView>>> GetHistoryAsync(string workItemKey, PageRequest page, CancellationToken ct);
}

/// <summary>One field saved from the drawer (FR-026).</summary>
public abstract record WorkItemEdit
{
    private WorkItemEdit()
    {
    }

    public sealed record Title(string Value) : WorkItemEdit;

    public sealed record Description(string? Value) : WorkItemEdit;

    public sealed record Priority(Domain.Work.Priority Value) : WorkItemEdit;

    public sealed record Status(long ColumnId) : WorkItemEdit;

    /// <summary>Null unassigns; otherwise an active Project Admin or Member (<c>NotAssignable</c>) (Phase 2 FR-016, FR-017).</summary>
    public sealed record Assignee(Guid? UserId) : WorkItemEdit;

    /// <summary>Both dates in one edit; either may be null (<c>InvalidDates</c>) (Phase 2 FR-018, FR-019).</summary>
    public sealed record Dates(DateOnly? Start, DateOnly? Due) : WorkItemEdit;
}

public sealed record WorkItemDetails(
    string Key,
    string ProjectKey,
    string ProjectName,
    WorkItemType Type,
    string Title,
    string? Description,
    Priority Priority,
    StatusOption Status,
    IReadOnlyList<StatusOption> Statuses,
    ParentRef? Parent,
    string CreatorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    bool CanDelete,
    byte[] Version,
    Page<SubtaskView> Subtasks,
    Page<CommentView> Comments,
    Page<ChangeView> History,
    AssigneeRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<AssigneeOption> AssigneeOptions,
    bool CanContribute = false);

public sealed record StatusOption(long Id, string Name, StatusCategory Category);

public sealed record ParentRef(string Key, string Title);

public sealed record SubtaskView(string Key, string Title, StatusOption Status, Priority Priority, byte[] Version,
    AssigneeRef? Assignee, DateOnly? DueDate);

/// <summary>A task's assignee as cards, rows and the drawer show them (Phase 2 FR-021, FR-024).</summary>
/// <param name="CanWork">False once they left the project, became a Viewer or were deactivated.</param>
public sealed record AssigneeRef(Guid UserId, string DisplayName, string Initials, bool CanWork)
{
    public static AssigneeRef Of(Guid userId, string displayName, bool canWork) =>
        new(userId, displayName, InitialsOf(displayName), canWork);

    /// <summary>The first letters of the first and last words ("Amina Khan" → "AK"), or the first two letters of a
    /// single word.</summary>
    public static string InitialsOf(string displayName)
    {
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => $"{words[0][0]}{words[^1][0]}",
        };
        return initials.ToUpperInvariant();
    }
}

/// <summary>Someone who can be assigned: an active Project Admin or Member (Phase 2 FR-017).</summary>
public sealed record AssigneeOption(Guid UserId, string DisplayName, bool IsMe);

/// <param name="Body">Null for a deleted comment, which shows as "comment deleted".</param>
public sealed record CommentView(
    long Id,
    Guid AuthorId,
    string AuthorName,
    bool IsMine,
    string? Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    byte[] Version);

public sealed record ChangeView(
    DateTimeOffset OccurredAt,
    string ActorName,
    WorkItemField Field,
    string? OldValue,
    string? NewValue,
    string? Note);

public sealed record DeletePreview(string Key, string Title, int SubtaskCount);

public sealed record DeletedItemView(string Key, string Title, DateTimeOffset DeletedAt, string DeletedByName, int SubtaskCount);
