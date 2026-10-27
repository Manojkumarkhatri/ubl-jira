namespace Upms.Application.Work.Contracts;

/// <summary>What the Projects module asks of the Work module when a project is created or switches template, inside its
/// own transaction (Phase 3 research R3).</summary>
public interface IProjectPlanSetup
{
    /// <summary>The four stages a new Stage-gate project starts with: Initiation, Planning, Execution and Closure, in
    /// that order, in the leftmost "to do" column (Phase 3 FR-003).</summary>
    Task CreateDefaultStagesAsync(long projectId, string projectKey, CancellationToken ct);

    /// <summary>True when a Scrum project has an active or planned sprint, which keeps it from becoming Kanban (FR-005).</summary>
    Task<bool> HasOpenSprintsAsync(long projectId, CancellationToken ct);

    /// <summary>Gives a project that becomes Scrum one order over all its cards, column by column from left to right and
    /// keeping each column's order, deleted cards included. No position on the board changes, so nothing is recorded
    /// (research R8).</summary>
    Task PrepareForScrumAsync(long projectId, CancellationToken ct);

    /// <summary>How many live and deleted items of the space have each type that can be turned off, so a type in use stays
    /// on (Jira parity pass FR-017).</summary>
    Task<IReadOnlyDictionary<Upms.Domain.Common.WorkTypeSet, int>> TypeCountsAsync(long projectId, CancellationToken ct);
}
