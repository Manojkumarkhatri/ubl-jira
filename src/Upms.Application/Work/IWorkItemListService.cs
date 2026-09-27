using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The List view: a project's tasks and sub-tasks, sorted, filtered and paged (Phase 2 FR-027–FR-032).</summary>
public interface IWorkItemListService
{
    /// <param name="today">The viewer's today, for "overdue" and "due in the next 7 days" (research R9).</param>
    Task<Result<WorkItemListView>> ListAsync(string projectKey, WorkItemListQuery query, DateOnly today, CancellationToken ct);
}

public enum ListSort
{
    Key,
    Title,
    Status,
    Priority,
    Assignee,
    StartDate,
    DueDate,
    Updated,
}

public enum DueFilter
{
    Any,

    /// <summary>Open and due before today.</summary>
    Overdue,

    /// <summary>Open and due from today to seven days after today, inclusive (FR-030).</summary>
    Next7Days,

    NoDueDate,
}

/// <summary>What the list shows. Filters of different kinds combine; the assignee filters ("me", "unassigned" and
/// people) match any of them. The default is the newest key first (FR-029).</summary>
/// <param name="Text">Words that must all appear in the title or the description.</param>
public sealed record WorkItemListQuery(
    ListSort Sort = ListSort.Key,
    bool Descending = true,
    IReadOnlyList<long>? ColumnIds = null,
    IReadOnlyList<StatusCategory>? Categories = null,
    IReadOnlyList<Priority>? Priorities = null,
    bool AssignedToMe = false,
    bool Unassigned = false,
    IReadOnlyList<Guid>? AssigneeIds = null,
    DueFilter Due = DueFilter.Any,
    string? Text = null,
    int Page = 1);

/// <param name="People">Everyone on the team and anyone else the project's tasks are assigned to, for the assignee
/// filter.</param>
/// <param name="FirstToDoColumnId">Where "What needs to be done?" creates tasks (FR-033).</param>
public sealed record WorkItemListView(
    string ProjectKey,
    string ProjectName,
    bool CanContribute,
    bool CanManage,
    bool CanRestoreDeleted,
    long FirstToDoColumnId,
    IReadOnlyList<StatusOption> Columns,
    IReadOnlyList<AssigneeOption> People,
    Page<WorkItemRow> Rows);

/// <param name="ParentKey">The parent task of a sub-task.</param>
/// <param name="IsOpen">Not in a "done" column, so it can be overdue.</param>
public sealed record WorkItemRow(
    string Key,
    string Title,
    string? ParentKey,
    StatusOption Status,
    Priority Priority,
    AssigneeRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAt,
    bool IsOpen);
