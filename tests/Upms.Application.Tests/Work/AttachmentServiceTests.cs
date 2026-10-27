using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>Attaching, listing, removing, purging and opening files (Phase 4 FR-023–FR-031, research R12, R13).</summary>
public sealed class AttachmentServiceTests(SqlServerFixture fixture) : AttachmentTestBase(fixture)
{
    [Fact]
    public async Task P4_US2_AS1_A_file_is_stored_as_being_checked_and_recorded_in_history()
    {
        var attached = await RequireAttachAsync(@"C:\Users\amina\Desktop\launch.png", Png);

        Assert.Equal(("launch.png", (long)Png.Length, "Amina Tester", AttachmentCheckStatus.Checking, true, true),
            (attached.FileName, attached.Size, attached.UploadedBy, attached.Status, attached.IsImage, attached.CanRemove));
        Assert.Equal(Png, await StoredBytesAsync(attached.Id));
        var change = (await HistoryAsync("WEB-1")).Items[^1];
        Assert.Equal((WorkItemField.AttachmentAdded, "launch.png"), (change.Field, change.NewValue));
        Assert.Equal([attached.Id], (await ListAsync()).Select(a => a.Id));
    }

    [Theory]
    [InlineData("setup.exe", FormattedNothing.Png, AttachmentRules.FileTypeNotAllowedCode)]
    [InlineData("shot.png", FormattedNothing.Html, AttachmentRules.FileContentMismatchCode)]
    [InlineData("notes.txt", FormattedNothing.Binary, AttachmentRules.FileContentMismatchCode)]
    public async Task P4_US2_AS4_Refused_files_store_nothing(string fileName, FormattedNothing content, string code)
    {
        byte[] bytes = content switch
        {
            FormattedNothing.Png => Png,
            FormattedNothing.Html => "<html><script>alert(1)</script></html>"u8.ToArray(),
            _ => [0x4D, 0x5A, 0x90, 0x00, 0x03],
        };

        var refused = await AttachAsync(fileName, bytes);

        Assert.Equal(code, refused.Error!.Code);
        Assert.Empty(await ListAsync());
        Assert.Equal(0, await QueryAsync(db => db.Attachments.CountAsync(Ct)));
    }

    [Fact]
    public async Task P4_US2_AS4_A_file_over_25_MB_is_refused_whatever_size_it_claims()
    {
        var big = new byte[(25 * 1024 * 1024) + 1];
        Pdf.CopyTo(big, 0);

        Assert.Equal(AttachmentRules.FileTooLargeCode, (await AttachAsync("big.pdf", big, size: 30_000_000)).Error!.Code);
        Assert.Equal(AttachmentRules.FileTooLargeCode, (await AttachAsync("big.pdf", big, size: 1_000)).Error!.Code);
        Assert.Equal(0, await QueryAsync(db => db.Attachments.CountAsync(Ct)));
    }

    [Fact]
    public async Task P4_US2_AS6_The_uploader_removes_a_file_with_history_and_the_bytes_kept()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);

        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(attached.Id, Ct))).IsSuccess);

        Assert.Empty(await ListAsync());
        Assert.Equal(Pdf, await StoredBytesAsync(attached.Id));
        var change = (await HistoryAsync("WEB-1")).Items[^1];
        Assert.Equal((WorkItemField.AttachmentRemoved, "report.pdf"), (change.Field, change.OldValue));
        Assert.Equal(AttachmentRules.NotAvailableCode, (await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(attached.Id, Ct))).Error!.Code);
    }

    [Fact]
    public async Task P4_US2_AS6_Project_Admins_and_administrators_remove_anyones_file_and_other_people_cannot()
    {
        var bilal = await MemberAsync("bilal");
        var omar = await MemberAsync("omar");
        ActAs(bilal);
        var first = await RequireAttachAsync("first.pdf", Pdf);
        var second = await RequireAttachAsync("second.pdf", Pdf);
        var third = await RequireAttachAsync("third.pdf", Pdf);

        ActAs(omar);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(first.Id, Ct))).Error!.Kind);
        ActAs(Owner);
        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(first.Id, Ct))).IsSuccess);
        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(second.Id, Ct))).IsSuccess);

        // An uploader who became a Viewer can no longer contribute, so cannot remove their file (rule 2).
        await Data.RemoveMemberAsync(await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.Id).SingleAsync(Ct)), bilal.Id);
        await Data.MembersAsync("WEB", ProjectRole.Viewer, bilal);
        ActAs(bilal);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IAttachmentService, Result>(s => s.RemoveAsync(third.Id, Ct))).Error!.Kind);
    }

    [Fact]
    public async Task P4_US2_AS7_Only_administrators_purge_and_the_bytes_go_with_an_audit_event()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IAttachmentService, Result>(s => s.PurgeAsync(attached.Id, Ct))).Error!.Kind);
        var admin = await Data.AdministratorAsync();
        ActAs(admin);

        Assert.True((await CallAsync<IAttachmentService, Result>(s => s.PurgeAsync(attached.Id, Ct))).IsSuccess);

        Assert.Null(await StoredBytesAsync(attached.Id));
        var purged = await QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync(a => a.Id == attached.Id, Ct));
        Assert.Equal(admin.Id, purged.PurgedById);
        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.AttachmentPurged);
        Assert.Equal(("WEB-1", admin.Id), (audit.Target, audit.ActorUserId!.Value));
        Assert.Contains("report.pdf", audit.Details, StringComparison.Ordinal);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task P4_US2_AS8_Viewers_see_the_list_but_cannot_attach_or_remove()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        var listed = Assert.Single(await ListAsync());

        Assert.Equal((attached.Id, false, false), (listed.Id, listed.CanRemove, listed.CanPurge));
        Assert.Equal(ErrorKind.Forbidden, (await AttachAsync("mine.pdf", Pdf)).Error!.Kind);
    }

    [Fact]
    public async Task P4_US2_AS9_A_file_opens_only_once_it_passed_its_check_and_for_people_who_can_see_it()
    {
        var attached = await RequireAttachAsync("launch.png", Png);
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id)).Error!.Kind);

        await ScanAsync();
        var opened = (await OpenAsync(attached.Id)).ValueOrThrow();
        Assert.Equal(("launch.png", "image/png", (long)Png.Length), (opened.FileName, opened.ContentType, opened.Size));
        Assert.Equal(Png, await ReadAllAsync(opened));
        Assert.True((await OpenAsync(attached.Id, preview: true)).IsSuccess);

        ActAs(await Data.UserAsync("nora"));
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id)).Error!.Kind);
        ActAs(Owner);
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(999_999)).Error!.Kind);
    }

    [Fact]
    public async Task Only_images_have_a_preview()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);
        await ScanAsync();

        Assert.True((await OpenAsync(attached.Id)).IsSuccess);
        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id, preview: true)).Error!.Kind);
    }

    [Fact]
    public async Task P4_US2_AS10_A_deleted_items_files_are_unreachable_until_it_is_restored()
    {
        var attached = await RequireAttachAsync("report.pdf", Pdf);
        await ScanAsync();
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).IsSuccess);

        Assert.Equal(ErrorKind.NotFound, (await OpenAsync(attached.Id)).Error!.Kind);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync("WEB-1", Ct))).IsSuccess);
        Assert.True((await OpenAsync(attached.Id)).IsSuccess);
    }

    [Fact]
    public async Task P4_FR023_Images_in_formatted_text_must_be_attachments_of_the_same_item()
    {
        var mine = await RequireAttachAsync("launch.png", Png);
        await CreateAsync(WorkItemType.Task, "Another task");
        var other = await RequireAttachAsync("other.png", Png, key: "WEB-2");
        JsonImage(mine.Id, out var mineImage);
        JsonImage(other.Id, out var otherImage);

        var saved = (await EditAsync("WEB-1", new WorkItemEdit.Description(Of(mineImage, otherImage)))).ValueOrThrow();

        var kept = FormattedText.Parse(saved.Description!.Content, "Description").Value!;
        Assert.Equal([mine.Id], kept.ImageAttachmentIds);
        Assert.Equal([mine.Id], (await RequireDetailsAsync("WEB-1")).Attachments.Select(a => a.Id));
    }

    private static void JsonImage(long attachmentId, out System.Text.Json.Nodes.JsonObject image) =>
        image = new() { ["type"] = "image", ["attrs"] = new System.Text.Json.Nodes.JsonObject { ["attachmentId"] = attachmentId, ["alt"] = "Screenshot" } };

    /// <summary>Content for the refusal theory.</summary>
    public enum FormattedNothing
    {
        Png,
        Html,
        Binary,
    }
}
