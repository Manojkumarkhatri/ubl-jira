using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Notifications;

/// <summary>The bell and its panel (Phase 4 FR-018–FR-020): one's own notifications only, and only about projects one can
/// still see.</summary>
public interface INotificationService
{
    Task<int> CountUnreadAsync(CancellationToken ct);

    /// <summary>Newest first, 20 a page; <c>Forbidden</c> when nobody active is signed in.</summary>
    Task<Result<Page<NotificationView>>> ListAsync(PageRequest page, CancellationToken ct);

    /// <summary>Marks one notification read or unread; <c>NotFound</c> for anyone else's.</summary>
    Task<Result> MarkAsync(long notificationId, bool read, CancellationToken ct);

    Task<Result> MarkAllReadAsync(CancellationToken ct);
}

/// <summary>One notification as the panel shows it, with the item's current key and title.</summary>
/// <param name="ActorName">Null when the system acted.</param>
public sealed record NotificationView(long Id, NotificationKind Kind, string? ActorName, string ProjectKey, string ItemKey,
    string ItemTitle, string? Detail, DateTimeOffset OccurredAt, bool IsRead);

/// <summary>Deletes notifications older than 90 days (FR-021); run daily by a background worker.</summary>
public interface INotificationCleanup
{
    /// <returns>How many notifications were deleted.</returns>
    Task<int> RunAsync(CancellationToken ct);
}
