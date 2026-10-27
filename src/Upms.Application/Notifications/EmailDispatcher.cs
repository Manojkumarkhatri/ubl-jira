using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Notifications;

namespace Upms.Application.Notifications;

/// <summary>Sends the emails that are due (Phase 4 FR-049–FR-052, research R10); <c>EmailDispatchWorker</c> calls it every
/// 15 seconds.</summary>
public interface IEmailDispatcher
{
    /// <returns>How many emails were tried.</returns>
    Task<int> SendDueAsync(CancellationToken ct);
}

/// <summary>Claims due emails so that two instances never send the same one, builds each from its notification at that
/// moment, sends it to the address on the account now, and retries or gives up on failure.</summary>
internal sealed partial class EmailDispatcher(
    IAppDbContext db,
    IEmailTransport transport,
    IUserDirectory users,
    IProjectTeam team,
    IWorkItemTitles titles,
    IContentAuditLog audit,
    IOptions<EmailOptions> options,
    TimeProvider time,
    ILogger<EmailDispatcher> logger) : IEmailDispatcher
{
    private const int BatchSize = 20;

    /// <summary>A claimed email comes back to the queue if its send never finishes (the instance stopped).</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        if (!transport.IsConfigured)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var leaseEnds = now + Lease;
        var claimed = await db.Database.SqlQuery<long>($"""
            UPDATE TOP ({BatchSize}) [EmailMessages] WITH (READPAST, ROWLOCK)
            SET [LockedUntil] = {leaseEnds}
            OUTPUT inserted.[Id] AS [Value]
            WHERE [SentAt] IS NULL AND [GivenUpAt] IS NULL AND [CancelledAt] IS NULL AND [NextAttemptAt] <= {now}
                AND ([LockedUntil] IS NULL OR [LockedUntil] <= {now})
            """).ToListAsync(ct);
        foreach (var id in claimed)
        {
            await SendAsync(id, ct);
        }

        return claimed.Count;
    }

    private async Task SendAsync(long emailId, CancellationToken ct)
    {
        var email = await db.EmailMessages.Include(e => e.Notification).SingleAsync(e => e.Id == emailId, ct);
        var notification = email.Notification!;
        var recipient = (await users.GetRecipientsAsync([email.RecipientId], ct)).GetValueOrDefault(email.RecipientId);

        // Someone deactivated, who no longer wants this kind of email or who can no longer see the project gets nothing.
        var project = recipient is { IsActive: true } && RecipientRules.IsEmailed(notification.Kind, recipient.EmailChoice)
            ? (await team.VisibleAmongAsync(recipient.Id, [notification.ProjectId], ct)) is [var visible, ..] ? visible : null
            : null;
        if (project is null)
        {
            email.Cancel(time.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return;
        }

        var item = (await titles.GetAsync([notification.WorkItemId], ct)).GetValueOrDefault(notification.WorkItemId);
        var actor = notification.ActorId is { } actorId ? (await users.GetAsync([actorId], ct)).GetValueOrDefault(actorId)?.DisplayName : null;
        var facts = new EmailFacts(notification.Kind, actor, project.Key, project.Name, item?.Key ?? "", item?.Title ?? "", notification.Detail);
        var settings = options.Value;
        var (subject, text, html) = EmailComposer.Compose(facts, settings.PublicBaseUrl);
        try
        {
            await transport.SendAsync(new OutgoingEmail(settings.FromAddress, settings.FromName, recipient!.Email, recipient.DisplayName,
                subject, text, html), ct);
            email.Sent(time.GetUtcNow());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (email.Failed($"{e.GetType().Name}: {e.Message}", time.GetUtcNow()))
            {
                // 24 hours without delivery (FR-052): the audit log says so; the in-app notification stays.
                await audit.WriteAsync(ContentAuditEvent.EmailGivenUp, recipient!.Id.ToString(),
                    new { Notification = notification.Id, Kind = notification.Kind.ToString(), Item = item?.Key, email.Attempts }, ct);
                LogGivenUp(logger, email.Id, email.Attempts);
            }
            else
            {
                LogRetrying(logger, email.Id, email.NextAttemptAt, e);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {EmailId} could not be sent; it will be tried again at {NextAttemptAt}.")]
    private static partial void LogRetrying(ILogger logger, long emailId, DateTimeOffset nextAttemptAt, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email {EmailId} was given up after {Attempts} attempts in 24 hours.")]
    private static partial void LogGivenUp(ILogger logger, long emailId, int attempts);
}
