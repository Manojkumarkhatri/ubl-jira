using Upms.Application.Notifications.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Tells watchers that their items changed status (Phase 4 FR-015), from every path that changes one: board moves,
/// the drawer, "mark done" and column deletion, the same paths that check the stage lock. Call it before saving.</summary>
internal sealed class StatusNotices(INotificationCenter notifications, WatchList watchers)
{
    public async Task RaiseAsync(IReadOnlyCollection<WorkItem> items, string statusName, Guid actorId, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        var watching = await watchers.OfAsync(items.Select(i => i.Id).ToList(), ct);
        await notifications.RaiseAsync(
            items.Select(i => new NotificationEvent(NotificationKind.StatusChanged, i.ProjectId, i.Id, actorId, watching[i.Id].ToList(), statusName))
                .ToList(),
            ct);
    }
}
