using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The malware check (Phase 4 FR-025, SC-003, research R14). Each run claims files whose check is due, so two
/// app instances never check the same file, then passes clean files, quarantines infected ones (the uploader is told
/// and the audit log records it) and leaves the rest "being checked" to try again: it fails closed.</summary>
internal sealed partial class AttachmentScanner(
    IAppDbContext db,
    IAttachmentContentStore store,
    IMalwareScanner scanner,
    INotificationCenter notifications,
    IContentAuditLog audit,
    TimeProvider time,
    ILogger<AttachmentScanner> logger) : IAttachmentScanner
{
    private const int BatchSize = 20;

    /// <summary>A claimed file comes back to the queue if its check never finishes (the instance stopped).</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    public async Task<int> ScanDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var leaseEnds = now + Lease;
        var claimed = await db.Database.SqlQuery<long>($"""
            UPDATE TOP ({BatchSize}) [Attachments] WITH (READPAST, ROWLOCK)
            SET [NextCheckAt] = {leaseEnds}
            OUTPUT inserted.[Id] AS [Value]
            WHERE [CheckStatus] = 'Checking' AND [RemovedAt] IS NULL AND [PurgedAt] IS NULL AND [NextCheckAt] <= {now}
            """).ToListAsync(ct);
        foreach (var id in claimed)
        {
            await CheckAsync(id, ct);
        }

        return claimed.Count;
    }

    private async Task CheckAsync(long attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.SingleAsync(a => a.Id == attachmentId, ct);
        ScanOutcome outcome;
        try
        {
            await using var content = await store.OpenReadAsync(attachmentId, ct);
            outcome = content is null ? ScanOutcome.Unavailable("The file's content is missing.") : await scanner.ScanAsync(content, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            outcome = ScanOutcome.Unavailable(e.GetType().Name);
        }

        var now = time.GetUtcNow();
        switch (outcome.Verdict)
        {
            case ScanVerdict.Clean:
                attachment.Pass(now);
                break;
            case ScanVerdict.Infected:
                attachment.Block(outcome.Detail ?? "Unknown threat", now);
                var item = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
                    .Where(w => w.Id == attachment.WorkItemId)
                    .Select(w => new { w.Key, w.ProjectId })
                    .SingleAsync(ct);
                await notifications.RaiseAsync(
                    [new NotificationEvent(NotificationKind.UploadBlocked, item.ProjectId, attachment.WorkItemId, null,
                        [attachment.UploadedById], attachment.FileName)], ct);
                LogBlocked(logger, attachment.Id, attachment.ThreatName!);
                await audit.WriteAsync(ContentAuditEvent.MalwareDetected, item.Key,
                    new { attachment.FileName, Sha256 = Convert.ToHexString(attachment.Sha256), Signature = attachment.ThreatName }, ct);
                break;
            default:
                attachment.CheckFailed(now);
                LogUnavailable(logger, attachment.Id, attachment.CheckAttempts, outcome.Detail);
                break;
        }

        await db.SaveChangesAsync(ct);
    }

    // The log holds ids, never file names or content (constitution: observability).
    [LoggerMessage(Level = LogLevel.Warning, Message = "Attachment {AttachmentId} was blocked by the malware check: {Threat}.")]
    private static partial void LogBlocked(ILogger logger, long attachmentId, string threat);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attachment {AttachmentId} could not be checked (attempt {Attempt}): {Reason}. It stays being checked.")]
    private static partial void LogUnavailable(ILogger logger, long attachmentId, int attempt, string? reason);
}
