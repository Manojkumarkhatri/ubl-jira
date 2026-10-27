namespace Upms.Domain.Work;

public enum SprintScopeKind
{
    /// <summary>In the sprint when it started.</summary>
    Committed,

    /// <summary>Moved in, or created on the board, while the sprint was active.</summary>
    Added,

    /// <summary>Moved out, or deleted, while the sprint was active.</summary>
    Removed,

    /// <summary>Done when the sprint was completed.</summary>
    Completed,

    /// <summary>Open when the sprint was completed.</summary>
    NotCompleted,
}

/// <summary>One line of a sprint's scope log, append-only, from which the sprint report is read (Phase 3 FR-029;
/// research R11). The estimate is the card's at that moment, so later edits do not change the report.</summary>
public sealed class SprintScopeEvent
{
    private SprintScopeEvent()
    {
    }

    public long Id { get; private set; }

    public long SprintId { get; private set; }

    public long WorkItemId { get; private set; }

    public SprintScopeKind Kind { get; private set; }

    public decimal? Estimate { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid ActorId { get; private set; }

    public static SprintScopeEvent Create(long sprintId, long workItemId, SprintScopeKind kind, decimal? estimate,
        ChangeContext context) => new()
        {
            SprintId = sprintId,
            WorkItemId = workItemId,
            Kind = kind,
            Estimate = estimate,
            OccurredAt = context.At,
            ActorId = context.ActorId,
        };
}
