using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>A purge is done by the current user; malware detections and emails given up are the system's doing, so they
/// have no actor whoever happens to be signed in.</summary>
internal sealed class ContentAuditLog(IAuditLog auditLog, Common.ICurrentUser currentUser) : IContentAuditLog
{
    public Task WriteAsync(ContentAuditEvent change, string target, object details, CancellationToken ct) => change switch
    {
        ContentAuditEvent.AttachmentPurged => auditLog.WriteAsync(AuditEventType.AttachmentPurged, currentUser.UserId, null, target, details, ct),
        ContentAuditEvent.MalwareDetected => auditLog.WriteAsync(AuditEventType.MalwareDetected, null, null, target, details, ct),
        _ => auditLog.WriteAsync(AuditEventType.EmailGivenUp, null, null, target, details, ct),
    };
}
