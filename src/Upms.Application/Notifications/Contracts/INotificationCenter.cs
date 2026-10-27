using Upms.Domain.Common;

namespace Upms.Application.Notifications.Contracts;

/// <summary>The one way into the Notifications module (Phase 4 research R1, R8). The Work module raises the events of one
/// action before it saves; the notifications are saved with the action, so a refused save notifies no one.</summary>
public interface INotificationCenter
{
    /// <summary>Adds one notification per recipient to the caller's unit of work: never the actor, never anyone who cannot
    /// see the project now, and one reason per person (a mention, then an assignment, then a watch) (FR-014, FR-017).</summary>
    Task RaiseAsync(IReadOnlyCollection<NotificationEvent> action, CancellationToken ct);
}

/// <summary>One reason to notify people about an action.</summary>
/// <param name="WorkItemId">The item, or the stage for gate events.</param>
/// <param name="ActorId">Who acted; null when the system did (a blocked upload).</param>
/// <param name="Detail">The new status, the gate's outcome or the blocked file's name.</param>
public sealed record NotificationEvent(NotificationKind Kind, long ProjectId, long WorkItemId, Guid? ActorId,
    IReadOnlyCollection<Guid> Recipients, string? Detail = null);
