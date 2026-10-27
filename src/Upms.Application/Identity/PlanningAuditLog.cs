using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

internal sealed class PlanningAuditLog(IAuditLog auditLog) : IPlanningAuditLog
{
    public Task WriteAsync(PlanningAuditEvent change, string target, object details, CancellationToken ct) =>
        auditLog.WriteAsync(change switch
        {
            PlanningAuditEvent.TemplateChanged => AuditEventType.ProjectTemplateChanged,
            PlanningAuditEvent.GateSetupChanged => AuditEventType.GateSetupChanged,
            PlanningAuditEvent.GateApprovalRequested => AuditEventType.GateApprovalRequested,
            PlanningAuditEvent.GateRequestWithdrawn => AuditEventType.GateRequestWithdrawn,
            PlanningAuditEvent.GateDecisionRecorded => AuditEventType.GateDecisionRecorded,
            PlanningAuditEvent.WorkTypesChanged => AuditEventType.ProjectWorkTypesChanged,
            _ => AuditEventType.GateApproved,
        }, null, target, details, ct);
}
