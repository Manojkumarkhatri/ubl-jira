using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 4 User Story 1 end to end: a formatted comment with a list, a link and a mention, the bell in a second
/// browser, watching, a status change heard by the watchers but not by the person who made it, and a table in the
/// description; with axe scans of the editor, the suggestion list and the notification panel.</summary>
public sealed class P4_US1_DiscussAndNotifyTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Editor(IPage page, string id) => page.Locator($"#{id} .rte-editable");

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

    private static async Task OpenDrawerAsync(IPage page, string projectKey, string key)
    {
        await GotoAsync(page, $"/spaces/{projectKey}/board?task={key}");
        await Assertions.Expect(page.GetByTestId("drawer-key")).ToHaveTextAsync(key);
    }

    [Fact]
    public async Task P4_US1_Independent_test_discuss_with_formatting_and_mentions_and_hear_about_it()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p4u1", "Amina Khan");
        var bilalPassword = await App.CreateUserAsync("bilal.p4u1", "Bilal Ahmed");
        var omarPassword = await App.CreateUserAsync("omar.p4u1", "Omar Ali");
        var amina = await SignInAsync("amina.p4u1", aminaPassword);
        await GotoAsync(amina, "/spaces");
        await amina.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = amina.GetByTestId("create-project");
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Launch Notes");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("LNC");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(amina, "/spaces/LNC/board");
        await App.AddMemberAsync("LNC", "bilal.p4u1");
        await App.AddMemberAsync("LNC", "omar.p4u1");
        await App.AddTasksAsync("LNC", [("Plan the launch", null, null)]);

        // P4_US1_AS1, AS5: a comment typed with shortcuts: bold, a bulleted list, a link (Ctrl+K) and "@bi" for Bilal.
        await OpenDrawerAsync(amina, "LNC", "LNC-1");
        await amina.GetByTestId("start-comment").ClickAsync();
        var editor = Editor(amina, "new-comment");
        await Assertions.Expect(editor).ToBeFocusedAsync();
        await amina.Keyboard.PressAsync("Control+B");
        await amina.Keyboard.TypeAsync("Launch");
        await amina.Keyboard.PressAsync("Control+B");
        await amina.Keyboard.TypeAsync(" checklist");
        await amina.Keyboard.PressAsync("Enter");
        await amina.Keyboard.TypeAsync("- Book the venue");
        await amina.Keyboard.PressAsync("Enter");
        await amina.Keyboard.PressAsync("Enter");
        await amina.Keyboard.PressAsync("Control+K");
        await amina.GetByLabel("Link address").FillAsync("https://example.com/plan");
        await amina.GetByLabel("Link address").PressAsync("Enter");
        await amina.Keyboard.TypeAsync(" ask @bi");
        var suggestions = amina.GetByRole(AriaRole.Listbox, new() { Name = "People to mention" });
        await Assertions.Expect(suggestions.GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
        await Assertions.Expect(suggestions.GetByRole(AriaRole.Option).First).ToContainTextAsync("Bilal Ahmed");
        await amina.AssertNoAccessibilityViolationsAsync(); // the editor with its suggestion list open
        await amina.Keyboard.PressAsync("Enter");
        await amina.GetByTestId("new-comment-save").ClickAsync();

        var comment = amina.GetByTestId("comment").Last;
        await Assertions.Expect(comment.Locator("strong")).ToHaveTextAsync("Launch");
        await Assertions.Expect(comment.Locator("li")).ToHaveTextAsync("Book the venue");
        await Assertions.Expect(comment.Locator("a[href='https://example.com/plan']")).ToHaveAttributeAsync("rel", "noopener noreferrer nofollow");
        await Assertions.Expect(comment.Locator(".mention")).ToHaveTextAsync("@Bilal Ahmed");
        await Assertions.Expect(amina.GetByTestId("watch-toggle")).ToHaveTextAsync("Stop watching"); // she commented

        // P4_US1_AS6: in another browser, Bilal's bell counts the mention, and opening it opens LNC-1.
        var bilal = await SignInAsync("bilal.p4u1", bilalPassword);
        await GotoAsync(bilal, "/my-tasks");
        var bell = bilal.GetByTestId("bell");
        await Assertions.Expect(bell).ToHaveAttributeAsync("aria-label", "Notifications, 1 unread");
        await bell.ClickAsync();
        var panel = bilal.Locator("#notification-panel");
        await Assertions.Expect(panel.Locator(".notification a")).ToHaveTextAsync("Amina Khan mentioned you on LNC-1 Plan the launch");
        await bilal.AssertNoAccessibilityViolationsAsync();
        await panel.Locator(".notification a").ClickAsync();
        await WaitForPathAsync(bilal, "/spaces/LNC/board?task=LNC-1");
        await Assertions.Expect(bilal.GetByTestId("drawer-key")).ToHaveTextAsync("LNC-1");
        await Assertions.Expect(bell).ToHaveAttributeAsync("aria-label", "Notifications, 0 unread");

        // P4_US1_AS9: Omar watches LNC-1; Bilal moves it to Done; Amina and Omar hear about it, Bilal does not.
        var omar = await SignInAsync("omar.p4u1", omarPassword);
        await OpenDrawerAsync(omar, "LNC", "LNC-1");
        await omar.GetByTestId("watch-toggle").ClickAsync();
        await Assertions.Expect(omar.GetByTestId("watch-toggle")).ToHaveTextAsync("Stop watching");
        await bilal.Locator("#drawer-status").SelectOptionAsync(new SelectOptionValue { Label = "Done" });
        await Assertions.Expect(bilal.GetByTestId("drawer-saved")).ToContainTextAsync("Status changed to Done");
        foreach (var watcher in new[] { amina, omar })
        {
            await GotoAsync(watcher, "/spaces/LNC/board"); // the drawer covers the header's bell
            await watcher.GetByTestId("bell").ClickAsync();
            await Assertions.Expect(watcher.Locator("#notification-panel .notification a").First)
                .ToHaveTextAsync("Bilal Ahmed moved LNC-1 Plan the launch to Done");
        }

        await bilal.ReloadAsync();
        await WaitForInteractivityAsync(bilal);
        await Assertions.Expect(bilal.GetByTestId("bell")).ToHaveAttributeAsync("aria-label", "Notifications, 0 unread");

        // P4_US1_AS2: a table in the description, with a row added from the table menu.
        await OpenDrawerAsync(amina, "LNC", "LNC-1");
        await amina.GetByTestId("edit-description").ClickAsync();
        var description = Editor(amina, "drawer-description");
        await Assertions.Expect(description).ToBeFocusedAsync();
        await amina.Locator("#drawer-description [data-cmd=table]").ClickAsync();
        await amina.Keyboard.TypeAsync("Step");
        await amina.Keyboard.PressAsync("Tab");
        await amina.Keyboard.TypeAsync("Owner");
        await amina.Locator("#drawer-description [data-cmd=addRowAfter]").ClickAsync();
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.GetByTestId("drawer-description-save").ClickAsync();
        var table = amina.GetByTestId("drawer-description").Locator("table");
        await Assertions.Expect(table.Locator("thead th")).ToHaveTextAsync(["Step", "Owner", ""]);
        await Assertions.Expect(table.Locator("tbody tr")).ToHaveCountAsync(3);

        // Escape then Tab leaves the editor (research R5).
        await amina.GetByTestId("start-comment").ClickAsync();
        await Assertions.Expect(Editor(amina, "new-comment")).ToBeFocusedAsync();
        await amina.Keyboard.PressAsync("Escape");
        await amina.Keyboard.PressAsync("Tab");
        await Assertions.Expect(amina.GetByTestId("new-comment-save")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task P4_US1_The_editor_and_panel_have_no_accessibility_violations_at_360_px()
    {
        var password = await App.CreateUserAsync("sara.p4u1", "Sara Malik");
        var phone = await SignInAsync("sara.p4u1", password);
        await CreateProjectAsync(phone, "Phone Plan", "LNP");
        await App.AddTasksAsync("LNP", [("Plan on a phone", null, null)]);
        await phone.SetViewportSizeAsync(360, 740);

        await OpenDrawerAsync(phone, "LNP", "LNP-1");
        await phone.GetByTestId("start-comment").ClickAsync();
        await Assertions.Expect(Editor(phone, "new-comment")).ToBeFocusedAsync();
        await phone.AssertNoAccessibilityViolationsAsync();
        var width = await phone.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        Assert.True(width <= 360, $"The page scrolls sideways at 360 px ({width} px).");

        await phone.Keyboard.PressAsync("Escape");
        await phone.Keyboard.PressAsync("Escape");
        await phone.GetByTestId("bell").ClickAsync();
        await Assertions.Expect(phone.Locator("#notification-panel")).ToContainTextAsync("No notifications yet.");
        await phone.AssertNoAccessibilityViolationsAsync();
        Assert.Matches(new Regex("^Notifications, \\d+ unread$"), await phone.GetByTestId("bell").GetAttributeAsync("aria-label"));
    }
}
