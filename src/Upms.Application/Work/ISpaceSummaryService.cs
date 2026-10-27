using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A space's Summary page, with Jira's summary sections (Jira parity pass FR-022; research R8).</summary>
public interface ISpaceSummaryService
{
    /// <summary>View right. "Due soon" counts open items due on or before <paramref name="today"/> plus 7 days, overdue ones
    /// included; the other counts cover the last 7 days. Deleted items are never counted.</summary>
    Task<Result<SpaceSummary>> GetAsync(string spaceKey, DateOnly today, CancellationToken ct);
}

/// <param name="StatusOverview">Every status type, in board order, with its count (zero included).</param>
/// <param name="RecentActivity">The 20 latest history entries of the space's items in the last 30 days, newest first.</param>
/// <param name="Priorities">Every priority from Highest to Lowest, with its count.</param>
/// <param name="Types">The types the space has items of, in the Create form's order.</param>
/// <param name="Workload">Open items per person, most first, then "Unassigned" (a null assignee).</param>
/// <param name="Containers">Epics by key, or stages in stage order, with their progress.</param>
public sealed record SpaceSummary(
    string SpaceKey,
    string SpaceName,
    ProjectTemplate Template,
    SummaryCounts Last7Days,
    IReadOnlyList<CategoryCount> StatusOverview,
    IReadOnlyList<ActivityEntry> RecentActivity,
    IReadOnlyList<PriorityCount> Priorities,
    IReadOnlyList<TypeCount> Types,
    IReadOnlyList<WorkloadRow> Workload,
    IReadOnlyList<ContainerProgress> Containers,
    bool CanManage = false,
    bool CanRestoreDeleted = false)
{
    /// <summary>Every status in board order with its count (zero included), for the status donut.</summary>
    public IReadOnlyList<StatusCount> Statuses { get; init; } = [];
}

public sealed record StatusCount(string Name, StatusCategory Category, int Count);

public sealed record SummaryCounts(int Completed, int Updated, int Created, int DueSoon);

public sealed record CategoryCount(StatusCategory Category, int Count);

public sealed record PriorityCount(Priority Priority, int Count);

public sealed record TypeCount(WorkItemType Type, int Count);

public sealed record WorkloadRow(AssigneeRef? Assignee, int OpenCount);

public sealed record ContainerProgress(string Key, string Title, WorkItemType Type, Progress Progress);

public sealed record ActivityEntry(string ItemKey, string ItemTitle, WorkItemType ItemType, string ActorName, DateTimeOffset At,
    WorkItemField Field, string? OldValue, string? NewValue, string? Note)
{
    public Guid ActorId { get; init; }

    /// <summary>The item's status now, shown beside it as in Jira.</summary>
    public string ItemStatus { get; init; } = "";

    public StatusCategory ItemCategory { get; init; }
}
