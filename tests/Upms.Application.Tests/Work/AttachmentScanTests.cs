using System.Text;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The malware check before anyone can download a file (Phase 4 FR-025, SC-003, research R14): the scan worker
/// claims files being checked, passes clean ones, quarantines infected ones and retries while the scanner is down.</summary>
public sealed class AttachmentScanTests(SqlServerFixture fixture) : AttachmentTestBase(fixture)
{
    private static readonly byte[] Infected = Encoding.ASCII.GetBytes(FakeMalwareScanner.TestVirus);

    private Task<Attachment> RowAsync(long id) => QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync(a => a.Id == id, Ct));

    [Fact]
    public async Task A_clean_file_passes()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);

        Assert.Equal(1, await ScanAsync());

        var row = await RowAsync(attached.Id);
        Assert.Equal((AttachmentCheckStatus.Passed, (DateTimeOffset?)null), (row.CheckStatus, row.NextCheckAt));
        Assert.Equal(AttachmentCheckStatus.Passed, Assert.Single(await ListAsync()).Status);
        Assert.Equal(0, await ScanAsync());
    }

    [Fact]
    public async Task P4_US2_AS5_An_infected_file_is_quarantined_its_uploader_told_and_the_detection_audited()
    {
        var bilal = await MemberAsync("bilal");
        ActAs(bilal);
        var attached = await RequireAttachAsync("eicar.txt", Infected);

        await ScanAsync();

        var row = await RowAsync(attached.Id);
        Assert.Equal((AttachmentCheckStatus.Blocked, "Eicar-Test-Signature"), (row.CheckStatus, row.ThreatName));
        Assert.Equal(Infected, await StoredBytesAsync(attached.Id)); // quarantined, not deleted
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id)).Error!.Kind);
        Assert.Equal(AttachmentCheckStatus.Blocked, Assert.Single(await ListAsync()).Status);

        var notification = await QueryAsync(db => db.Notifications.AsNoTracking().SingleAsync(Ct));
        Assert.Equal((bilal.Id, NotificationKind.UploadBlocked, (Guid?)null, "eicar.txt"),
            (notification.RecipientId, notification.Kind, notification.ActorId, notification.Detail));
        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.MalwareDetected);
        Assert.Equal(("WEB-1", (Guid?)null), (audit.Target, audit.ActorUserId));
        Assert.Contains("Eicar-Test-Signature", audit.Details, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(row.Sha256), audit.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P4_SC003_While_the_scanner_is_down_files_stay_being_checked_and_are_retried_after_1_5_then_15_minutes()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);
        Scanner.Down = true;

        await ScanAsync();
        Assert.Equal(Harness.Time.GetUtcNow().AddMinutes(1), (await RowAsync(attached.Id)).NextCheckAt);
        Assert.Equal(0, await ScanAsync()); // not due yet

        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await ScanAsync();
        Assert.Equal(Harness.Time.GetUtcNow().AddMinutes(5), (await RowAsync(attached.Id)).NextCheckAt);

        Harness.Time.Advance(TimeSpan.FromMinutes(5));
        await ScanAsync();
        Assert.Equal(Harness.Time.GetUtcNow().AddMinutes(15), (await RowAsync(attached.Id)).NextCheckAt);
        Assert.Equal(AttachmentCheckStatus.Checking, (await RowAsync(attached.Id)).CheckStatus);
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id)).Error!.Kind);

        Scanner.Down = false;
        Harness.Time.Advance(TimeSpan.FromMinutes(15));
        await ScanAsync();
        Assert.Equal(AttachmentCheckStatus.Passed, (await RowAsync(attached.Id)).CheckStatus);
    }

    [Fact]
    public async Task Two_workers_never_check_the_same_file()
    {
        for (var i = 0; i < 6; i++)
        {
            await RequireAttachAsync($"report-{i}.pdf", Pdf);
        }

        Scanner.Delay = TimeSpan.FromMilliseconds(200);

        var counts = await Task.WhenAll(ScanAsync(), ScanAsync());

        Assert.Equal(6, counts.Sum());
        Assert.Equal(6, Scanner.Scans);
        Assert.All(await ListAsync(), a => Assert.Equal(AttachmentCheckStatus.Passed, a.Status));
    }

    [Fact]
    public async Task Removed_and_purged_files_are_not_checked()
    {
        var removed = await RequireAttachAsync("removed.pdf", Pdf);
        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(removed.Id, Ct))).IsSuccess);
        var purged = await RequireAttachAsync("purged.pdf", Pdf);
        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.PurgeAsync(purged.Id, Ct))).IsSuccess);

        Assert.Equal(0, await ScanAsync());
        Assert.Equal(0, Scanner.Scans);
    }
}
