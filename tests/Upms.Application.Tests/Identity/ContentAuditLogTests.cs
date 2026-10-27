using Upms.Application.Identity.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Malware detections, permanent deletions and emails given up reach the security audit log through the
/// Identity contract that the Work and Notifications modules use (Phase 4 research R20). A purge is done by the current
/// user; detections and emails given up are the system's doing and have no actor.</summary>
public sealed class ContentAuditLogTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    [Theory]
    [InlineData(ContentAuditEvent.MalwareDetected, AuditEventType.MalwareDetected, false)]
    [InlineData(ContentAuditEvent.AttachmentPurged, AuditEventType.AttachmentPurged, true)]
    [InlineData(ContentAuditEvent.EmailGivenUp, AuditEventType.EmailGivenUp, false)]
    public async Task Each_content_event_is_written_as_its_audit_event(ContentAuditEvent change, AuditEventType expected, bool byCaller)
    {
        var admin = await Data.AdministratorAsync();
        ActAs(admin);

        await Harness.CallAsync<IContentAuditLog>(log => log.WriteAsync(change, "WEB-1", new { FileName = "report.pdf" }, Ct));

        var written = Assert.Single(await AuditEventsAsync());
        Assert.Equal(expected, written.EventType);
        Assert.Equal(byCaller ? admin.Id : null, written.ActorUserId);
        Assert.Null(written.SubjectUserId);
        Assert.Equal("WEB-1", written.Target);
        Assert.Equal("{\"FileName\":\"report.pdf\"}", written.Details);
    }

    [Fact]
    public async Task Events_from_background_work_have_no_actor()
    {
        ActAs(null);

        await Harness.CallAsync<IContentAuditLog>(log => log.WriteAsync(ContentAuditEvent.MalwareDetected, "WEB-1",
            new { Signature = "Eicar-Test-Signature" }, Ct));

        var written = Assert.Single(await AuditEventsAsync());
        Assert.Equal((AuditEventType.MalwareDetected, (Guid?)null), (written.EventType, written.ActorUserId));
    }
}
