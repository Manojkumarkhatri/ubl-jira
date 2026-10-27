using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

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

/// <param name="Rows">Top-level items in no epic or stage that are scheduled or have scheduled sub-tasks, by first date,
/// due date and key; at most <see cref="MaxRows"/>, the latest ones when there are more. Epics and stages, and their
/// items, are in <see cref="Groups"/> (Phase 3 FR-042).</param>
/// <param name="ScheduledCount">How many rows there are before that limit.</param>
/// <param name="Unscheduled">Items in no epic or stage without dates.</param>
public sealed record TimelineView(
    string ProjectKey,
    string ProjectName,
    bool CanContribute,
    bool CanManage,
    bool CanRestoreDeleted,
    IReadOnlyList<TimelineRow> Rows,
    int ScheduledCount,
    Page<TimelineItem> Unscheduled,
    bool HidingCompleted,
    ProjectTemplate Template = ProjectTemplate.Kanban)
{
    /// <summary>The timeline shows up to 500 scheduled tasks at a time (spec assumptions, "Scale").</summary>
    public const int MaxRows = 500;

    /// <summary>At most 200 dependencies are drawn at a time (Phase 3 spec assumptions, "Scale").</summary>
    public const int MaxDependencies = 200;

    /// <summary>Epics or stages with their items, before the items in no container: stages in stage order, epics by first
    /// date, due date and key, undated ones last (Phase 3 FR-042).</summary>
    public IReadOnlyList<TimelineGroup> Groups { get; init; } = [];

    /// <summary>A Scrum project's active and completed sprints, oldest first (Phase 3 FR-046).</summary>
    public IReadOnlyList<SprintBar> Sprints { get; init; } = [];

    /// <summary>Dependencies whose two items are shown with dates (Phase 3 FR-050).</summary>
    public IReadOnlyList<TimelineDependency> Dependencies { get; init; } = [];

    /// <summary>The space's statuses in board order, for each row's Status menu.</summary>
    public IReadOnlyList<StatusOption> Statuses { get; init; } = [];
}

public sealed record TimelineRow(TimelineItem Task, IReadOnlyList<TimelineItem> ScheduledSubtasks, IReadOnlyList<TimelineItem> UnscheduledSubtasks);

/// <param name="Container">The epic or stage; without dates it keeps its row, without a bar.</param>
/// <param name="Gate">A stage's gate on its due date (Phase 3 FR-044).</param>
/// <param name="Unscheduled">Its items without dates.</param>
public sealed record TimelineGroup(TimelineItem Container, Progress Progress, GateMarker? Gate, IReadOnlyList<TimelineRow> Rows,
    IReadOnlyList<TimelineItem> Unscheduled);

public sealed record GateMarker(DateOnly Date, GateState State);

public sealed record SprintBar(string Name, DateOnly StartDate, DateOnly EndDate, SprintState State);

/// <param name="IsLate">The waiting item starts before the item it waits for is due, while that one is open (FR-051).</param>
public sealed record TimelineDependency(string WaitedForKey, string WaitingKey, bool IsLate);

/// <summary>An item the timeline item waits for, for its accessible name.</summary>
public sealed record WaitingOn(string Key, bool IsLate);

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
    public WorkItemType Type { get; init; } = WorkItemType.Task;

    /// <summary>An epic's color for its bar (Jira parity pass FR-025).</summary>
    public EpicColor? Color { get; init; }

    /// <summary>What it waits for, with late marks (Phase 3 FR-050, FR-051).</summary>
    public IReadOnlyList<WaitingOn> WaitsFor { get; init; } = [];

    public bool IsScheduled => StartDate is not null || DueDate is not null;

    /// <summary>The start date, or the due date when there is no start date (FR-035).</summary>
    public DateOnly? FirstDate => StartDate ?? DueDate;
}
