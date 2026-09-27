using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;
using Upms.Infrastructure.Persistence;

namespace Upms.Infrastructure.Identity;

/// <summary>Appends security events (FR-010). Events are saved immediately in their own statement so
/// that failures (for example a failed sign-in) are recorded even when nothing else is saved.</summary>
public sealed class AuditLog(
    AppDbContext db,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider time) : IAuditLog
{
    public Task WriteAsync(AuditEventType type, Guid? subjectUserId, string target, object? details, CancellationToken ct) =>
        WriteAsync(type, currentUser.UserId, subjectUserId, target, details, ct);

    public async Task WriteAsync(AuditEventType type, Guid? actorUserId, Guid? subjectUserId, string target,
        object? details, CancellationToken ct)
    {
        var auditEvent = new AuditEvent(
            type,
            time.GetUtcNow(),
            actorUserId,
            subjectUserId,
            target,
            details is null ? null : JsonSerializer.Serialize(details),
            httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(ct);
    }
}
