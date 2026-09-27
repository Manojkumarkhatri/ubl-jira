namespace Upms.Application.Work.Contracts;

/// <summary>Work items by status, for the Projects module's column rules (FR-037, FR-039).</summary>
public interface IWorkItemStatusMover
{
    /// <summary>Work items per status: tasks and sub-tasks, soft-deleted ones included.</summary>
    Task<IReadOnlyDictionary<long, int>> CountInStatusesAsync(IReadOnlyCollection<long> statusIds, CancellationToken ct);

    /// <summary>Moves every work item in <paramref name="fromStatusId"/> (sub-tasks and soft-deleted items
    /// included) to <paramref name="toStatusId"/> of the same project, recording each move in its history with
    /// <paramref name="note"/>. Tasks join the end of the destination column in their current order; moving into
    /// a "done" column marks them completed. The caller's next save commits the moves together with its own
    /// changes, in one transaction.</summary>
    Task MoveAllAsync(long fromStatusId, long toStatusId, string note, CancellationToken ct);
}
