using Upms.Domain.Identity;

namespace Upms.Application.Identity.Contracts;

/// <summary>Writes security events to the append-only audit log (FR-010). The actor is the current
/// user; the source IP is captured when available. Details must never contain passwords or tokens.</summary>
public interface IAuditLog
{
    Task WriteAsync(AuditEventType type, Guid? subjectUserId, string target, object? details, CancellationToken ct);

    /// <summary>Writes an event whose actor is not the current user (for example a sign-in).</summary>
    Task WriteAsync(AuditEventType type, Guid? actorUserId, Guid? subjectUserId, string target, object? details,
        CancellationToken ct);
}
