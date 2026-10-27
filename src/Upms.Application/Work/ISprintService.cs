using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Sprints of Scrum projects (Phase 3 FR-023, FR-025, FR-028, FR-029). Creating, editing, starting, completing
/// and deleting need the Manage right (Project Admins and administrators); reports need View. Stale versions return
/// <c>Conflict</c> with the current sprint.</summary>
public interface ISprintService
{
    /// <summary>A planned sprint named "<key> Sprint <n>".</summary>
    Task<Result<SprintSection>> CreateAsync(string projectKey, CancellationToken ct);

    /// <summary>Renames a planned or active sprint and changes its goal; <c>SprintCompleted</c>.</summary>
    Task<Result<SprintSection>> UpdateAsync(long sprintId, string name, string? goal, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Starts a planned sprint; the end defaults to 14 days after the start. <c>SprintAlreadyActive</c>,
    /// <c>InvalidDates</c>. Each card in it is recorded as committed.</summary>
    Task<Result<SprintSection>> StartAsync(long sprintId, DateOnly start, DateOnly? end, string? goal, byte[] expectedVersion,
        CancellationToken ct);

    /// <summary>Completes the active sprint. Done cards stay in it; open cards go to the backlog (null) or to a planned
    /// sprint.</summary>
    Task<Result<SprintSection>> CompleteAsync(long sprintId, long? moveOpenItemsToSprintId, byte[] expectedVersion,
        CancellationToken ct);

    /// <summary>Deletes a planned sprint; its cards return to the backlog in their order.</summary>
    Task<Result> DeleteAsync(long sprintId, byte[] expectedVersion, CancellationToken ct);

    /// <summary>A completed sprint's report, read from its scope log, so it never changes.</summary>
    Task<Result<SprintReport>> GetReportAsync(string projectKey, int sprintNumber, CancellationToken ct);
}

public sealed record SprintReport(string ProjectKey, string ProjectName, string Name, string? Goal, DateOnly StartDate,
    DateOnly EndDate, DateTimeOffset CompletedAt, ReportGroup Committed, ReportGroup Added, ReportGroup Removed,
    ReportGroup Completed, ReportGroup NotCompleted, bool CanManage, bool CanRestoreDeleted);

public sealed record ReportGroup(IReadOnlyList<ReportLine> Items, decimal EstimateTotal);

/// <param name="Estimate">The card's estimate when it was counted.</param>
public sealed record ReportLine(string Key, WorkItemType Type, string Title, decimal? Estimate, bool IsDeleted);
