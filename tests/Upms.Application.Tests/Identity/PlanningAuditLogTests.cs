using Upms.Application.Identity.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Template switches and gate events reach the security audit log through the Identity contract that the
/// Projects and Work modules use (Phase 3 research R17).</summary>
public sealed class PlanningAuditLogTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    [Theory]
    [InlineData(PlanningAuditEvent.TemplateChanged, AuditEventType.ProjectTemplateChanged)]
    [InlineData(PlanningAuditEvent.GateSetupChanged, AuditEventType.GateSetupChanged)]
    [InlineData(PlanningAuditEvent.GateApprovalRequested, AuditEventType.GateApprovalRequested)]
    [InlineData(PlanningAuditEvent.GateRequestWithdrawn, AuditEventType.GateRequestWithdrawn)]
    [InlineData(PlanningAuditEvent.GateDecisionRecorded, AuditEventType.GateDecisionRecorded)]
    [InlineData(PlanningAuditEvent.GateApproved, AuditEventType.GateApproved)]
    public async Task Each_planning_event_is_written_as_its_audit_event(PlanningAuditEvent change, AuditEventType expected)
    {
        var sara = await Data.UserAsync("sara");
        ActAs(sara);

        await Harness.CallAsync<IPlanningAuditLog>(log => log.WriteAsync(change, "CBU-1", new { Request = 2 }, Ct));

        var written = Assert.Single(await AuditEventsAsync());
        Assert.Equal(expected, written.EventType);
        Assert.Equal(sara.Id, written.ActorUserId);
        Assert.Null(written.SubjectUserId);
        Assert.Equal("CBU-1", written.Target);
        Assert.Equal("{\"Request\":2}", written.Details);
    }
}
