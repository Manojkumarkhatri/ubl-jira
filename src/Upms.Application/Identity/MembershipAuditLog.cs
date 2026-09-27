using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

internal sealed class MembershipAuditLog(IAuditLog auditLog) : IMembershipAuditLog
{
    public Task WriteAsync(MembershipChange change, Guid memberId, string projectKey, object details, CancellationToken ct) =>
        auditLog.WriteAsync(change switch
        {
            MembershipChange.Added => AuditEventType.MemberAdded,
            MembershipChange.Removed => AuditEventType.MemberRemoved,
            _ => AuditEventType.MemberRoleChanged,
        }, memberId, projectKey, details, ct);
}
