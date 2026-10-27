using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>"My tasks": the caller's open tasks and sub-tasks in the projects they can see (Phase 2 FR-025, FR-026).</summary>
public interface IMyTasksService
{
    /// <summary>Ordered by project name, then due date (undated last), priority and number.</summary>
    Task<Result<Page<MyTaskRow>>> ListAsync(PageRequest page, CancellationToken ct);

    /// <summary>Gates that await the caller's decision, in projects they can see, oldest request first (Phase 3 FR-041):
    /// pending requests where they are an approver who has not decided and is not the excluded requester.</summary>
    Task<Result<IReadOnlyList<WaitingGateRow>>> ListGatesAsync(CancellationToken ct);

    /// <summary>The Home page's view of the caller's work in the spaces they can see: how much is open, overdue and due
    /// in the next 7 days (counting today), what they finished in the last 7 days, the open work by status type and by
    /// space, and the first items due.</summary>
    Task<Result<MyWorkSummary>> SummaryAsync(DateOnly today, CancellationToken ct);
}

/// <param name="Upcoming">Open items overdue or due within 7 days, earliest first.</param>
/// <param name="Assigned">Open items, the earliest due first and undated last.</param>
public sealed record MyWorkSummary(int Open, int Overdue, int DueSoon, int DoneLast7Days, int ToDo, int InProgress,
    IReadOnlyList<MyTaskRow> Upcoming, IReadOnlyList<MyTaskRow> Assigned, IReadOnlyList<SpaceWorkCount> BySpace)
{
    public const int Listed = 8;

    public static MyWorkSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, [], [], []);
}

/// <summary>The caller's open items in one space.</summary>
public sealed record SpaceWorkCount(string ProjectKey, string ProjectName, int Open);

/// <summary>A gate waiting for the caller; it opens at <c>/spaces/{ProjectKey}/stages?stage={StageKey}</c>.</summary>
public sealed record WaitingGateRow(string ProjectKey, string ProjectName, string StageKey, string StageTitle, string RequestedBy,
    DateTimeOffset RequestedAt);

/// <param name="ParentKey">The parent task of a sub-task.</param>
/// <param name="Type">Every type of work item can be assigned (Phase 3 FR-041).</param>
public sealed record MyTaskRow(string ProjectKey, string ProjectName, string Key, string Title, string? ParentKey,
    StatusOption Status, Priority Priority, DateOnly? DueDate, WorkItemType Type = WorkItemType.Task);
