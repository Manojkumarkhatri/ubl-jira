using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's attachments (Phase 4 FR-023–FR-029; research R13, R25): the list with each file's state, previews
/// for checked images, downloads, removal and permanent deletion by right, and uploads announced as they go.</summary>
public sealed class AttachmentsSectionTests : BunitTestBase
{
    private readonly FakeAttachmentService _files = new();

    public AttachmentsSectionTests()
    {
        Services.AddSingleton<IAttachmentService>(_files);
    }

    private static AttachmentView File(long id, string name, AttachmentCheckStatus status, bool canRemove = false, bool canPurge = false,
        long size = 2_097_152) =>
        new(id, name, size, "Bilal Ahmed", Start, status, AttachmentRules.IsImage(AttachmentRules.ExtensionOf(name)), canRemove, canPurge);

    private IRenderedComponent<AttachmentsSection> Show(bool canAttach = true) =>
        Render<AttachmentsSection>(p => p
            .Add(x => x.WorkItemKey, "WEB-1")
            .Add(x => x.Initial, _files.Items.ToList())
            .Add(x => x.CanAttach, canAttach));

    [Fact]
    public void P4_FR026_Each_file_shows_its_name_size_uploader_time_and_state()
    {
        _files.Items.AddRange([
            File(1, "launch.png", AttachmentCheckStatus.Passed),
            File(2, "report.pdf", AttachmentCheckStatus.Passed, size: 1_536),
            File(3, "new.xlsx", AttachmentCheckStatus.Checking),
            File(4, "eicar.txt", AttachmentCheckStatus.Blocked),
        ]);

        var cut = Show();

        var items = cut.FindAll("[data-testid=attachment]");
        Assert.Equal(4, items.Count);
        Assert.Contains("2.0 MB", items[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("1.5 KB", items[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Bilal Ahmed", items[0].TextContent, StringComparison.Ordinal);
        Assert.NotNull(items[0].QuerySelector("time"));
        Assert.Equal("attachments/1/preview", items[0].QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("Enlarge launch.png", items[0].QuerySelector("button.attachment-thumb")!.GetAttribute("aria-label"));
        Assert.Equal(("attachments/2", "Download report.pdf"),
            (items[1].QuerySelector("a")!.GetAttribute("href"), items[1].QuerySelector("a")!.GetAttribute("aria-label")));
        Assert.Contains("Being checked", items[2].TextContent, StringComparison.Ordinal);
        Assert.Null(items[2].QuerySelector("a"));
        Assert.Contains("Blocked by the malware check", items[3].TextContent, StringComparison.Ordinal);
        Assert.Null(items[3].QuerySelector("a"));
    }

    [Fact]
    public void A_checked_image_can_be_enlarged()
    {
        _files.Items.Add(File(1, "launch.png", AttachmentCheckStatus.Passed));
        var cut = Show();

        cut.Find("button.attachment-thumb").Click();

        var dialog = cut.Find("[data-testid=attachment-preview]");
        Assert.Equal(("attachments/1/preview", "launch.png"), (dialog.QuerySelector("img")!.GetAttribute("src"), dialog.QuerySelector("img")!.GetAttribute("alt")));
    }

    [Fact]
    public void P4_US2_AS6_Remove_asks_first()
    {
        _files.Items.Add(File(2, "report.pdf", AttachmentCheckStatus.Passed, canRemove: true));
        var cut = Show();

        cut.Find("[aria-label='Remove report.pdf']").Click();
        Assert.Empty(_files.Removed);
        cut.Find("[data-testid=confirm-remove]").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=attachment]")));
        Assert.Equal([2L], _files.Removed);
        Assert.Equal("report.pdf removed.", cut.Find("[data-testid=attachments] [role=status]").TextContent);
    }

    [Fact]
    public void P4_US2_AS7_Administrators_delete_permanently_after_confirming()
    {
        _files.Items.Add(File(2, "report.pdf", AttachmentCheckStatus.Passed, canRemove: true, canPurge: true));
        var cut = Show();

        cut.Find("[aria-label='Delete report.pdf permanently']").Click();
        Assert.Contains("can't be undone", cut.Find("[data-testid=attachment]").TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=confirm-purge]").Click();

        cut.WaitForAssertion(() => Assert.Equal([2L], _files.Purged));
    }

    [Fact]
    public void P4_US2_AS8_Viewers_see_files_without_attach_remove_or_delete()
    {
        _files.Items.Add(File(2, "report.pdf", AttachmentCheckStatus.Passed));

        var cut = Show(canAttach: false);

        Assert.Empty(cut.FindAll("[data-testid=attach-files]"));
        Assert.Empty(cut.FindAll("[aria-label='Remove report.pdf']"));
        Assert.Empty(cut.FindAll("[aria-label='Delete report.pdf permanently']"));
        Assert.NotNull(cut.Find("[aria-label='Download report.pdf']"));
    }

    [Fact]
    public void P4_FR023_Attach_files_opens_the_file_chooser()
    {
        var cut = Show();

        cut.Find("[data-testid=attach-files]").Click();

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.attachments.choose");
    }

    [Fact]
    public async Task P4_US2_AS1_An_upload_is_announced_and_listed_as_being_checked()
    {
        var cut = Show();

        await cut.InvokeAsync(() => cut.Instance.UploadAsync("report.pdf", 4, new FakeStreamReference("%PDF")));

        Assert.Equal(("WEB-1", "report.pdf", 4L, "%PDF"), (_files.Added[0].Key, _files.Added[0].FileName, _files.Added[0].Size,
            Encoding.ASCII.GetString(_files.Added[0].Bytes)));
        Assert.Contains("Being checked", cut.Find("[data-testid=attachment]").TextContent, StringComparison.Ordinal);
        Assert.Equal("report.pdf attached. It is being checked for malware.", cut.Find("[data-testid=attachments] [role=status]").TextContent);
    }

    [Fact]
    public async Task P4_US2_AS4_A_refused_upload_says_why()
    {
        _files.Refuse = _ => AppError.Rule(AttachmentRules.FileTypeNotAllowedCode, "This type of file can't be attached.");
        var cut = Show();

        await cut.InvokeAsync(() => cut.Instance.UploadAsync("setup.exe", 4, new FakeStreamReference("MZ")));

        Assert.Equal("setup.exe: This type of file can't be attached.", cut.Find("[data-testid=attachments] [role=alert]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=attachment]"));
    }

    [Fact]
    public async Task P4_US2_AS4_A_file_over_25_MB_is_refused_before_it_is_sent()
    {
        var cut = Show();
        var huge = new FakeStreamReference("x");

        await cut.InvokeAsync(() => cut.Instance.UploadAsync("video.png", 30_000_000, huge));

        Assert.Equal("video.png: Files can be up to 25 MB.", cut.Find("[data-testid=attachments] [role=alert]").TextContent);
        Assert.False(huge.Opened);
        Assert.Empty(_files.Added);
    }

    [Fact]
    public void Files_being_checked_refresh_until_the_check_is_done()
    {
        _files.Items.Add(File(3, "new.pdf", AttachmentCheckStatus.Checking));
        var cut = Show();

        _files.Items[0] = _files.Items[0] with { Status = AttachmentCheckStatus.Passed };
        Time.Advance(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[aria-label='Download new.pdf']")));
        var lists = _files.Lists;
        Time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(lists, _files.Lists); // nothing left to check
    }

    private sealed class FakeStreamReference(string content) : IJSStreamReference
    {
        public bool Opened { get; private set; }

        public long Length => content.Length;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Opened = true;
            return ValueTask.FromResult<Stream>(new MemoryStream(Encoding.ASCII.GetBytes(content)));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
