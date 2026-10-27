using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>A space's Reports, as Jira's "Created vs resolved" report: work created and resolved each day, and the open work
/// left at the end of each day.</summary>
public interface ISpaceReportService
{
    /// <summary>View right. <paramref name="days"/> whole UTC days up to today (7 to 365). Deleted items are never counted.</summary>
    Task<Result<CreatedVsResolved>> CreatedVsResolvedAsync(string spaceKey, int days, CancellationToken ct);
}

/// <param name="Days">One row per day, oldest first.</param>
public sealed record CreatedVsResolved(IReadOnlyList<DayCounts> Days, int CreatedTotal, int ResolvedTotal);

/// <param name="Open">Live items still open at the end of the day.</param>
/// <param name="LeadDays">For items resolved that day, the average days from created to resolved.</param>
/// <param name="CycleDays">For items resolved that day, the average days from their first move into an "in progress" status
/// to resolved; items that never went through one are left out.</param>
public sealed record DayCounts(DateOnly Day, int Created, int Resolved, int Open, double? LeadDays = null, double? CycleDays = null);
