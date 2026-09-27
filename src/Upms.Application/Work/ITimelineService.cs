using Upms.Application.Common;
using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>The Timeline view: scheduled tasks as bars, the unscheduled ones beside them, and rescheduling
/// (Phase 2 FR-034–FR-040).</summary>
public interface ITimelineService
{
    /// <param name="hideCompleted">Leaves out tasks and sub-tasks in "done" columns (FR-040).</param>
    Task<Result<TimelineView>> GetAsync(string projectKey, bool hideCompleted, CancellationToken ct);

    /// <summary>Top-level tasks with no dates and no scheduled sub-tasks, by key, 50 at a time (FR-036).</summary>
    Task<Result<Page<TimelineItem>>> ListUnscheduledAsync(string projectKey, bool hideCompleted, PageRequest page, CancellationToken ct);

    /// <summary>Sets both dates as one edit, with the rules and conflict check of <see cref="WorkItemEdit.Dates"/>:
    /// <c>InvalidDates</c>, and <c>Conflict</c> carrying the current item (FR-037–FR-039).</summary>
    Task<Result<TimelineItem>> RescheduleAsync(string workItemKey, DateOnly? start, DateOnly? due, byte[] expectedVersion,
        CancellationToken ct);
}

/// <param name="Rows">Top-level tasks that are scheduled or have scheduled sub-tasks, by first date, due date and key;
/// at most <see cref="MaxRows"/>, the latest ones when there are more.</param>
/// <param name="ScheduledCount">How many rows there are before that limit.</param>
public sealed record TimelineView(
    string ProjectKey,
    string ProjectName,
    bool CanContribute,
    bool CanManage,
    bool CanRestoreDeleted,
    IReadOnlyList<TimelineRow> Rows,
    int ScheduledCount,
    Page<TimelineItem> Unscheduled,
    bool HidingCompleted)
{
    /// <summary>The timeline shows up to 500 scheduled tasks at a time (spec assumptions, "Scale").</summary>
    public const int MaxRows = 500;
}

public sealed record TimelineRow(TimelineItem Task, IReadOnlyList<TimelineItem> ScheduledSubtasks, IReadOnlyList<TimelineItem> UnscheduledSubtasks);

/// <param name="IsOpen">Not in a "done" column.</param>
/// <param name="Version">The row version it was read with; rescheduling sends it back.</param>
public sealed record TimelineItem(
    string Key,
    string Title,
    StatusOption Status,
    AssigneeRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    bool IsOpen,
    byte[] Version)
{
    public bool IsScheduled => StartDate is not null || DueDate is not null;

    /// <summary>The start date, or the due date when there is no start date (FR-035).</summary>
    public DateOnly? FirstDate => StartDate ?? DueDate;
}
