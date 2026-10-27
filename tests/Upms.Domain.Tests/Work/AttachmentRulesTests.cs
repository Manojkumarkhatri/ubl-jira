using System.IO.Compression;
using System.Text;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Which files can be attached, how they are named, and the life of an attachment (Phase 4 FR-023–FR-029,
/// data model, research R13, R14).</summary>
public sealed class AttachmentRulesTests
{
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);

    private static MemoryStream Text(string text, bool bom = false) =>
        new([.. (bom ? Encoding.UTF8.GetPreamble() : []), .. Encoding.UTF8.GetBytes(text)]);

    private static MemoryStream Zip(params string[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
                writer.Write("<x/>");
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static Attachment Uploaded() =>
        Attachment.Upload(7, "report.pdf", 1_024, new byte[32], Amina, T0).Value!;

    [Theory]
    [InlineData("shot.png")]
    [InlineData("PHOTO.JPG")]
    [InlineData("photo.jpeg")]
    [InlineData("anim.gif")]
    [InlineData("pic.webp")]
    [InlineData("report.pdf")]
    [InlineData("old.doc")]
    [InlineData("new.docx")]
    [InlineData("old.xls")]
    [InlineData("new.xlsx")]
    [InlineData("old.ppt")]
    [InlineData("new.pptx")]
    [InlineData("notes.txt")]
    [InlineData("server.log")]
    [InlineData("data.csv")]
    [InlineData("config.json")]
    [InlineData("feed.xml")]
    [InlineData("deploy.yaml")]
    [InlineData("deploy.yml")]
    [InlineData("README.md")]
    [InlineData("query.sql")]
    [InlineData("analysis.ipynb")]
    public void P4_FR024_The_allowed_types_can_be_attached(string fileName)
    {
        Assert.Null(AttachmentRules.CheckType(fileName));
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("script.js")]
    [InlineData("page.html")]
    [InlineData("image.svg")]
    [InlineData("archive.zip")]
    [InlineData("macro.docm")]
    [InlineData("noextension")]
    [InlineData("report.pdf.exe")]
    [InlineData(".png")]
    public void P4_US2_AS4_Other_types_are_refused(string fileName)
    {
        Assert.Equal(AttachmentRules.FileTypeNotAllowedCode, AttachmentRules.CheckType(fileName)?.Code);
    }

    [Fact]
    public void P4_US2_AS4_Files_over_25_MB_and_empty_files_are_refused()
    {
        Assert.Null(AttachmentRules.CheckSize(26_214_400));
        Assert.Equal(AttachmentRules.FileTooLargeCode, AttachmentRules.CheckSize(26_214_401)?.Code);
        Assert.Equal("File", AttachmentRules.CheckSize(0)?.Field);
    }

    [Theory]
    [InlineData(@"C:\Users\amina\Desktop\report.pdf", "report.pdf")]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("  spaced name.png  ", "spaced name.png")]
    [InlineData("bell\u0007and\u0000nul\r\n.txt", "bellandnul.txt")]
    public void File_names_lose_paths_and_control_characters(string uploaded, string kept)
    {
        Assert.Equal(kept, AttachmentRules.CleanFileName(uploaded));
    }

    [Fact]
    public void Long_file_names_are_cut_to_255_characters_keeping_the_extension()
    {
        var kept = AttachmentRules.CleanFileName(new string('a', 300) + ".pdf");

        Assert.Equal(255, kept.Length);
        Assert.EndsWith(".pdf", kept, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 })]
    [InlineData("jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0 })]
    [InlineData("jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0 })]
    [InlineData("gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0 })]
    [InlineData("webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]
    [InlineData("pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 })]
    [InlineData("doc", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0 })]
    [InlineData("xls", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0 })]
    [InlineData("ppt", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0 })]
    public void P4_FR024_Binary_files_must_start_as_their_type_does(string extension, byte[] head)
    {
        Assert.Null(AttachmentRules.CheckContent(extension, Bytes(head)));
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Text("<html><script>alert(1)</script>"))?.Code);
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    public void P4_FR024_Office_files_are_zip_packages_with_content_types(string extension)
    {
        Assert.Null(AttachmentRules.CheckContent(extension, Zip("[Content_Types].xml", "word/document.xml")));
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Zip("evil.js"))?.Code);
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Bytes(0x50, 0x4B, 3, 4, 0, 0))?.Code);
    }

    [Theory]
    [InlineData("txt")]
    [InlineData("log")]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("yaml")]
    [InlineData("yml")]
    [InlineData("md")]
    [InlineData("sql")]
    [InlineData("ipynb")]
    public void P4_FR024_Text_and_technical_files_are_UTF8_without_NUL_bytes(string extension)
    {
        Assert.Null(AttachmentRules.CheckContent(extension, Text("SELECT 1; -- café ✓")));
        Assert.Null(AttachmentRules.CheckContent(extension, Text("with a byte order mark", bom: true)));
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Bytes(0x41, 0x00, 0x42))?.Code);
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Bytes(0x41, 0xC3, 0x28))?.Code);
        Assert.Equal(AttachmentRules.FileContentMismatchCode, AttachmentRules.CheckContent(extension, Bytes(0x4D, 0x5A, 0x90, 0x00))?.Code);
    }

    [Theory]
    [InlineData("png", "image/png", true)]
    [InlineData("jpg", "image/jpeg", true)]
    [InlineData("jpeg", "image/jpeg", true)]
    [InlineData("gif", "image/gif", true)]
    [InlineData("webp", "image/webp", true)]
    [InlineData("pdf", "application/octet-stream", false)]
    [InlineData("txt", "application/octet-stream", false)]
    [InlineData("xml", "application/octet-stream", false)]
    public void Images_keep_their_type_and_everything_else_is_a_download(string extension, string contentType, bool isImage)
    {
        Assert.Equal((contentType, isImage), (AttachmentRules.ContentTypeOf(extension), AttachmentRules.IsImage(extension)));
    }

    [Fact]
    public void P4_US2_AS1_An_upload_starts_being_checked()
    {
        var attachment = Uploaded();

        Assert.Equal((AttachmentCheckStatus.Checking, "pdf", "application/octet-stream", T0, 0), (attachment.CheckStatus,
            attachment.Extension, attachment.ContentType, attachment.NextCheckAt, attachment.CheckAttempts));
        Assert.False(attachment.IsAvailable);
    }

    [Fact]
    public void A_clean_file_passes_and_becomes_available()
    {
        var attachment = Uploaded();

        attachment.Pass(T0.AddMinutes(1));

        Assert.Equal((AttachmentCheckStatus.Passed, (DateTimeOffset?)null, (DateTimeOffset?)T0.AddMinutes(1), 1),
            (attachment.CheckStatus, attachment.NextCheckAt, attachment.CheckedAt, attachment.CheckAttempts));
        Assert.True(attachment.IsAvailable);
    }

    [Fact]
    public void P4_US2_AS5_An_infected_file_is_blocked_and_never_available()
    {
        var attachment = Uploaded();

        attachment.Block("Eicar-Test-Signature", T0.AddMinutes(1));

        Assert.Equal((AttachmentCheckStatus.Blocked, "Eicar-Test-Signature", (DateTimeOffset?)null),
            (attachment.CheckStatus, attachment.ThreatName, attachment.NextCheckAt));
        Assert.False(attachment.IsAvailable);
    }

    [Fact]
    public void P4_SC003_A_scanner_that_is_down_retries_after_1_5_and_then_every_15_minutes()
    {
        var attachment = Uploaded();

        attachment.CheckFailed(T0);
        Assert.Equal(T0.AddMinutes(1), attachment.NextCheckAt);
        attachment.CheckFailed(T0.AddMinutes(1));
        Assert.Equal(T0.AddMinutes(6), attachment.NextCheckAt);
        attachment.CheckFailed(T0.AddMinutes(6));
        Assert.Equal(T0.AddMinutes(21), attachment.NextCheckAt);
        attachment.CheckFailed(T0.AddMinutes(21));
        Assert.Equal(T0.AddMinutes(36), attachment.NextCheckAt);
        Assert.Equal((AttachmentCheckStatus.Checking, 4), (attachment.CheckStatus, attachment.CheckAttempts));
    }

    [Fact]
    public void P4_US2_AS6_A_removed_file_keeps_its_record_and_is_no_longer_available()
    {
        var attachment = Uploaded();
        attachment.Pass(T0);

        Assert.Null(attachment.Remove(Amina, T0.AddHours(1)));

        Assert.Equal(((DateTimeOffset?)T0.AddHours(1), (Guid?)Amina), (attachment.RemovedAt, attachment.RemovedById));
        Assert.False(attachment.IsAvailable);
        Assert.Equal(AttachmentRules.NotAvailableCode, attachment.Remove(Amina, T0.AddHours(2))?.Code);
    }

    [Fact]
    public void A_blocked_file_cannot_be_removed_only_purged()
    {
        var attachment = Uploaded();
        attachment.Block("Eicar-Test-Signature", T0);

        Assert.Equal(AttachmentRules.NotAvailableCode, attachment.Remove(Amina, T0)?.Code);
        Assert.Null(attachment.Purge(Admin, T0.AddDays(1)));
    }

    [Fact]
    public void P4_US2_AS7_A_purge_is_final()
    {
        var attachment = Uploaded();
        attachment.Pass(T0);
        attachment.Remove(Amina, T0.AddHours(1));

        Assert.Null(attachment.Purge(Admin, T0.AddDays(1)));

        Assert.Equal(((DateTimeOffset?)T0.AddDays(1), (Guid?)Admin), (attachment.PurgedAt, attachment.PurgedById));
        Assert.Equal(AttachmentRules.NotAvailableCode, attachment.Purge(Admin, T0.AddDays(2))?.Code);
        Assert.False(attachment.IsAvailable);
    }
}
