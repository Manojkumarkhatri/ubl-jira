using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Notifications;

namespace Upms.Application.Notifications;

internal sealed class NotificationCenter(IAppDbContext db, IProjectTeam team, IUserDirectory users, IEmailTransport email, TimeProvider time)
    : INotificationCenter
{
    public async Task RaiseAsync(IReadOnlyCollection<NotificationEvent> action, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var created = new List<Notification>();
        foreach (var project in action.GroupBy(e => e.ProjectId))
        {
            // One reason per person and item: an action such as deleting a column can move many items at once.
            var chosen = project
                .GroupBy(e => e.WorkItemId)
                .Select(item => (Events: item.ToList(), Recipients: RecipientRules.Resolve(item.Select(e => (e.Kind, e.Recipients)), item.First().ActorId)))
                .Where(item => item.Recipients.Count > 0)
                .ToList();
            if (chosen.Count == 0)
            {
                continue;
            }

            // Who can see the project is asked once for everyone the action reaches (FR-014).
            var seeing = await team.WhoCanSeeAsync(project.Key, chosen.SelectMany(c => c.Recipients.Keys).Distinct().ToList(), ct);
            foreach (var (events, recipients) in chosen)
            {
                foreach (var (recipient, kind) in recipients.Where(r => seeing.Contains(r.Key)))
                {
                    var reason = events.First(e => e.Kind == kind && e.Recipients.Contains(recipient));
                    var notification = Notification.Create(recipient, kind, reason.ProjectId, reason.WorkItemId, reason.ActorId, reason.Detail, now);
                    db.Notifications.Add(notification);
                    created.Add(notification);
                }
            }
        }

        await QueueEmailsAsync(created, now, ct);
    }

    /// <summary>The outbox (FR-049, research R10, R11): an email for each notification the recipient's choice includes, in the
    /// same unit of work, so the action never waits for the mail server. Nothing is queued while email is off.</summary>
    private async Task QueueEmailsAsync(List<Notification> created, DateTimeOffset now, CancellationToken ct)
    {
        if (!email.IsConfigured || created.Count == 0)
        {
            return;
        }

        var recipients = await users.GetRecipientsAsync(created.Select(n => n.RecipientId).Distinct().ToList(), ct);
        foreach (var notification in created)
        {
            if (recipients.TryGetValue(notification.RecipientId, out var recipient) && recipient.IsActive
                && RecipientRules.IsEmailed(notification.Kind, recipient.EmailChoice))
            {
                db.EmailMessages.Add(EmailMessage.For(notification, now));
            }
        }
    }
}
