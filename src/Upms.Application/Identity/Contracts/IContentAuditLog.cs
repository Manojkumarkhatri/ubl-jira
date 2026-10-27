namespace Upms.Application.Identity.Contracts;

/// <summary>Records malware detections, permanent deletions of attachments and emails given up in the security audit log
/// for the Work and Notifications modules (Phase 4 FR-055, research R20), as <see cref="IPlanningAuditLog"/> does for
/// Phase 3. The actor is the current user, or none for background work.</summary>
public interface IContentAuditLog
{
    /// <param name="target">The item key for attachment events; the recipient's id for an email given up.</param>
    /// <param name="details">Serialized into the event; never file contents, descriptions or comments.</param>
    Task WriteAsync(ContentAuditEvent change, string target, object details, CancellationToken ct);
}

public enum ContentAuditEvent
{
    MalwareDetected,
    AttachmentPurged,
    EmailGivenUp,
}
