using Upms.Domain.Common;

namespace Upms.Domain.Notifications;

/// <summary>One person's in-app notification about one action (Phase 4 FR-015–FR-021, research R8). Read and listed only
/// by its recipient, and only while they can see the project; deleted after 90 days.</summary>
public sealed class Notification
{
    public const int DetailMaxLength = 200;

    private Notification()
    {
    }

    public long Id { get; private set; }

    public Guid RecipientId { get; private set; }

    public NotificationKind Kind { get; private set; }

    public long ProjectId { get; private set; }

    /// <summary>The item, or the stage for gate events.</summary>
    public long WorkItemId { get; private set; }

    /// <summary>Null only when the system acted (a blocked upload).</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>The new status, the gate's outcome or the blocked file's name.</summary>
    public string? Detail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Null while unread.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(Guid recipientId, NotificationKind kind, long projectId, long workItemId, Guid? actorId,
        string? detail, DateTimeOffset occurredAt) => new()
        {
            RecipientId = recipientId,
            Kind = kind,
            ProjectId = projectId,
            WorkItemId = workItemId,
            ActorId = actorId,
            Detail = detail is { Length: > DetailMaxLength } ? detail[..(DetailMaxLength - 1)] + "…" : detail,
            OccurredAt = occurredAt,
        };

    /// <summary>Keeps the first time it was read.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    public void MarkUnread() => ReadAt = null;
}
