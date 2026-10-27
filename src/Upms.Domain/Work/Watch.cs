namespace Upms.Domain.Work;

/// <summary>A person watching a work item (Phase 4 FR-011–FR-014, research R7): a personal subscription, not a change to
/// the item, so it is not recorded in history. Rows stay when people lose access; they are skipped until they can see
/// the item again.</summary>
public sealed class Watch
{
    private Watch()
    {
    }

    public long WorkItemId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public static Watch Start(long workItemId, Guid userId, DateTimeOffset now) =>
        new() { WorkItemId = workItemId, UserId = userId, StartedAt = now };
}
