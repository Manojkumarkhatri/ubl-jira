namespace Upms.Application.Work.Contracts;

/// <summary>Work item counts for other modules (FR-013 open task counts).</summary>
public interface IWorkItemCounts
{
    /// <summary>Non-deleted work items (tasks and sub-tasks) per status.</summary>
    Task<IReadOnlyDictionary<long, int>> CountByStatusAsync(IReadOnlyCollection<long> statusIds, CancellationToken ct);
}
