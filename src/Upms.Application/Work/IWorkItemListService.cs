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

    /// <summary>By type name (Phase 3 FR-020).</summary>
    Type,

    /// <summary>By when the item was created, as Jira's "Created" column.</summary>
    Created,
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
/// people) match any of them, and so do the container filters (epics or stages, and "none"). The default is the newest
/// key first (FR-029).</summary>
/// <param name="Text">Words that must all appear in the title or the description.</param>
/// <param name="Types">Phase 3 FR-020: only these types.</param>
/// <param name="ContainerKeys">Phase 3 FR-020: only items in these epics or stages.</param>
/// <param name="NoContainer">Phase 3 FR-020: items in no epic or stage.</param>
/// <param name="Labels">Phase 4 FR-046: items with any of these labels.</param>
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
    int Page = 1,
    IReadOnlyList<WorkItemType>? Types = null,
    IReadOnlyList<string>? ContainerKeys = null,
    bool NoContainer = false,
    IReadOnlyList<string>? Labels = null);

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
    Page<WorkItemRow> Rows,
    ProjectTemplate Template = ProjectTemplate.Kanban,
    WorkTypeSet WorkTypes = WorkTypeSet.All)
{
    /// <summary>The space's own fields, which the list can show as columns (live test, 2026-10-28).</summary>
    public IReadOnlyList<ListFieldColumn> Fields { get; init; } = [];
}

/// <summary>A space field the list can show as a column.</summary>
public sealed record ListFieldColumn(long Id, string Name);

/// <param name="ParentKey">The parent task of a sub-task.</param>
/// <param name="IsOpen">Not in a "done" column, so it can be overdue.</param>
/// <param name="Container">The epic or stage (Phase 3 FR-020).</param>
/// <param name="Labels">The item's labels (Phase 4 FR-045).</param>
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
    bool IsOpen,
    WorkItemType Type = WorkItemType.Task,
    ContainerView? Container = null,
    decimal? Estimate = null,
    IReadOnlyList<string>? Labels = null)
{
    public IReadOnlyList<string> Labels { get; init; } = Labels ?? [];

    /// <summary>Who created it, as Jira's "Reporter".</summary>
    public AssigneeRef? Reporter { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it was done; null while it is open ("Unresolved").</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>For changing it from the list.</summary>
    public byte[] Version { get; init; } = [];

    /// <summary>The item's space-field values in words, by field id; a field without a value is absent.</summary>
    public IReadOnlyDictionary<long, string> FieldText { get; init; } = new Dictionary<long, string>();
}
