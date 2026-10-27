using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;
using Upms.Infrastructure.Persistence;

namespace Upms.E2E.Tests;

/// <summary>Phase 4 User Story 2 end to end: a screenshot attached with the file chooser and another pasted into a comment,
/// both checked before anyone can open them, a Viewer downloading, oversized and disallowed files refused, and the antivirus
/// test file blocked, its uploader told and the audit log written; with axe scans of the list and a preview.</summary>
public sealed class P4_US2_AttachmentsTests(AppFixture app) : BrowserTest(app)
{
    // A real 1×1 PNG, padded to about 2 MB with an ancillary chunk browsers ignore.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static ILocator Files(IPage page) => page.GetByTestId("attachments");

    private static Task ChooseAsync(IPage page, string name, string mimeType, byte[] content) =>
        Files(page).Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = name, MimeType = mimeType, Buffer = content });

    private static async Task CreateProjectAsync(IPage page, string name, string key)
    {
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync(name);
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, $"/spaces/{key}/board");
    }

    [Fact]
    public async Task P4_US2_Independent_test_attach_check_and_download_files()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p4u2", "Amina Khan");
        var bilalPassword = await App.CreateUserAsync("bilal.p4u2", "Bilal Viewer");
        var amina = await SignInAsync("amina.p4u2", aminaPassword);
        await CreateProjectAsync(amina, "Attachments Check", "ATT");
        await App.AddMemberAsync("ATT", "bilal.p4u2", ProjectRole.Viewer);
        await App.AddTasksAsync("ATT", [("Design the home page", null, null)]);
        await GotoAsync(amina, "/spaces/ATT/board?task=ATT-1");
        await Assertions.Expect(amina.GetByTestId("drawer-key")).ToHaveTextAsync("ATT-1");

        // P4_US2_AS1: a screenshot from the file chooser is "being checked", then previewable.
        var screenshot = Png.Concat(new byte[2 * 1024 * 1024]).ToArray();
        await ChooseAsync(amina, "home.png", "image/png", screenshot);
        var file = Files(amina).GetByTestId("attachment").Filter(new() { HasText = "home.png" });
        await Assertions.Expect(file).ToContainTextAsync("Being checked");
        await Assertions.Expect(file.Locator("img")).ToBeVisibleAsync(new() { Timeout = 40_000 });
        await Assertions.Expect(file).ToContainTextAsync("2.0 MB");

        // P4_US2_AS2: a screenshot pasted into a comment is attached and shows in the text once checked.
        await amina.GetByTestId("start-comment").ClickAsync();
        await amina.Locator("#new-comment .rte-editable").EvaluateAsync("""
            (editable, bytes) => {
                const data = new DataTransfer();
                data.items.add(new File([new Uint8Array(bytes)], 'pasted.png', { type: 'image/png' }));
                editable.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
            }
            """, Png.Select(b => (int)b).ToArray());
        await Assertions.Expect(amina.Locator("#new-comment .rte-editable img[data-attachment-id]")).ToHaveCountAsync(1);
        await amina.GetByTestId("new-comment-save").ClickAsync();
        await Assertions.Expect(amina.GetByTestId("comment").Last.Locator(".formatted-text img")).ToBeVisibleAsync(new() { Timeout = 40_000 });

        // P4_US2_AS4: a 30 MB file and a program are refused, and nothing is stored.
        await ChooseAsync(amina, "video.png", "image/png", new byte[30 * 1024 * 1024]);
        await Assertions.Expect(Files(amina).GetByRole(AriaRole.Alert)).ToHaveTextAsync("video.png: Files can be up to 25 MB.");
        await ChooseAsync(amina, "setup.exe", "application/octet-stream", [0x4D, 0x5A]);
        await Assertions.Expect(Files(amina).GetByRole(AriaRole.Alert)).ToContainTextAsync("setup.exe: This type of file can't be attached.");

        // P4_US2_AS5: the antivirus test file is blocked, Amina is told, and the audit log records it.
        await ChooseAsync(amina, "eicar.txt", "text/plain", Encoding.ASCII.GetBytes(TestVirusScanner.TestVirus));
        var blocked = Files(amina).GetByTestId("attachment").Filter(new() { HasText = "eicar.txt" });
        await Assertions.Expect(blocked).ToContainTextAsync("Blocked by the malware check", new() { Timeout = 40_000 });
        await Assertions.Expect(blocked.GetByRole(AriaRole.Link)).ToHaveCountAsync(0);
        await GotoAsync(amina, "/spaces/ATT/board"); // the drawer covers the header's bell
        await amina.GetByTestId("bell").ClickAsync();
        await Assertions.Expect(amina.Locator("#notification-panel .notification a").First)
            .ToHaveTextAsync("eicar.txt on ATT-1 was blocked by the malware check");
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.MalwareDetected && e.Target == "ATT-1", Ct));
        }

        await amina.Keyboard.PressAsync("Escape");
        await amina.AssertNoAccessibilityViolationsAsync();

        // P4_US2_AS8: Bilal, a Viewer, downloads the checked file but cannot attach or remove.
        var bilal = await SignInAsync("bilal.p4u2", bilalPassword);
        await GotoAsync(bilal, "/spaces/ATT/board?task=ATT-1");
        await Assertions.Expect(Files(bilal).GetByTestId("attach-files")).ToHaveCountAsync(0);
        await Assertions.Expect(Files(bilal).GetByRole(AriaRole.Button, new() { Name = "Remove home.png" })).ToHaveCountAsync(0);
        var download = await bilal.RunAndWaitForDownloadAsync(() =>
            Files(bilal).GetByRole(AriaRole.Link, new() { Name = "Download home.png" }).ClickAsync());
        Assert.Equal("home.png", download.SuggestedFilename);

        // An image can be enlarged; the preview dialog has no accessibility violations at 360 px either.
        await bilal.SetViewportSizeAsync(360, 740);
        await Files(bilal).GetByRole(AriaRole.Button, new() { Name = "Enlarge home.png" }).ClickAsync();
        await Assertions.Expect(bilal.GetByTestId("attachment-preview").Locator("img")).ToHaveAttributeAsync("alt", "home.png");
        await bilal.AssertNoAccessibilityViolationsAsync();
    }
}
