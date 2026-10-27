using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 4 User Story 5 end to end, with smtp4dev as the mail server: with the default choice Bilal gets emails for
/// an assignment and a mention but not for a comment on an item he watches; Omar, who chose "All notifications", also gets
/// that one; Farah, who chose "None", gets nothing; and an email's link opens the item after signing in; with axe scans of
/// the profile at 1280 px and 360 px.</summary>
public sealed class P4_US5_EmailTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

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

    private static async Task ChooseEmailsAsync(IPage page, string choice)
    {
        await GotoAsync(page, "/account/profile");
        await page.GetByRole(AriaRole.Radio, new() { Name = choice }).CheckAsync();
        await page.GetByTestId("save-email-choice").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=save-email-choice] ~ .saved")).ToHaveTextAsync("Saved");
    }

    private static async Task CommentAsync(IPage page, string text, string? mention = null)
    {
        // The box stays open after a comment is posted, ready for the next one.
        var editor = page.Locator("#new-comment .rte-editable");
        if (await page.GetByTestId("start-comment").CountAsync() > 0)
        {
            await page.GetByTestId("start-comment").ClickAsync();
        }
        else
        {
            await editor.ClickAsync();
        }

        await Assertions.Expect(editor).ToBeFocusedAsync(); // the editor loads first
        await page.Keyboard.TypeAsync(text);
        if (mention is not null)
        {
            await page.Keyboard.TypeAsync($" @{mention[..3]}");
            var suggestions = page.GetByRole(AriaRole.Listbox, new() { Name = "People to mention" });
            await Assertions.Expect(suggestions.GetByRole(AriaRole.Option).First).ToContainTextAsync(mention);
            await page.Keyboard.PressAsync("Enter");
        }

        await page.GetByTestId("new-comment-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("comment").Last).ToContainTextAsync(text);
    }

    /// <summary>Waits until the worker, which runs every 15 seconds, has sent <paramref name="count"/> emails to the address.</summary>
    private async Task<IReadOnlyList<(string Subject, string Text)>> EmailsAsync(string address, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        IReadOnlyList<(string Subject, string Text)> emails;
        while ((emails = await App.EmailsToAsync(address)).Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(1_000, Ct);
        }

        return emails;
    }

    [Fact]
    public async Task P4_US5_Independent_test_emails_follow_each_persons_choice()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p4u5", "Amina Mail");
        var bilalPassword = await App.CreateUserAsync("bilal.p4u5", "Bilal Mail");
        var omarPassword = await App.CreateUserAsync("omar.p4u5", "Omar Mail");
        var farahPassword = await App.CreateUserAsync("farah.p4u5", "Farah Mail");

        // Omar wants everything and Farah nothing; the profile has no accessibility violations at 1280 px and 360 px.
        var omar = await SignInAsync("omar.p4u5", omarPassword);
        await ChooseEmailsAsync(omar, "All notifications");
        await omar.AssertNoAccessibilityViolationsAsync();
        var farah = await SignInAsync("farah.p4u5", farahPassword);
        await farah.SetViewportSizeAsync(360, 740);
        await ChooseEmailsAsync(farah, "None");
        await farah.AssertNoAccessibilityViolationsAsync();

        var amina = await SignInAsync("amina.p4u5", aminaPassword);
        await CreateProjectAsync(amina, "Email Check", "EMW");
        foreach (var member in new[] { "bilal.p4u5", "omar.p4u5", "farah.p4u5" })
        {
            await App.AddMemberAsync("EMW", member);
        }

        await App.AddTasksAsync("EMW", [("Plan the launch", null, null)]);

        // P4_US5_AS1, AS2: an assignment and a mention reach Bilal, Omar and Farah's watch makes them hear about comments.
        await GotoAsync(amina, "/spaces/EMW/board?task=EMW-1");
        await Drawer(amina).Locator("#drawer-assignee").SelectOptionAsync(new SelectOptionValue { Label = "Bilal Mail" });
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Assigned to Bilal Mail");
        foreach (var watcher in new[] { (omar, "omar"), (farah, "farah") })
        {
            await GotoAsync(watcher.Item1, "/spaces/EMW/board?task=EMW-1");
            await watcher.Item1.GetByTestId("watch-toggle").ClickAsync();
            await Assertions.Expect(watcher.Item1.GetByTestId("watch-toggle")).ToHaveTextAsync("Stop watching");
        }

        await CommentAsync(amina, "Venue confirmed,", mention: "Bilal Mail");
        await CommentAsync(amina, "Catering booked too.");

        var bilalEmails = await EmailsAsync("bilal.p4u5@example.com", 2);
        Assert.Equal(["[EMW-1] Plan the launch", "[EMW-1] Plan the launch"], bilalEmails.Select(e => e.Subject));
        Assert.Contains(bilalEmails, e => e.Text.Contains("Amina Mail assigned EMW-1 Plan the launch to you.", StringComparison.Ordinal));
        Assert.Contains(bilalEmails, e => e.Text.Contains("Amina Mail mentioned you on EMW-1 Plan the launch.", StringComparison.Ordinal));
        Assert.All(bilalEmails, e => Assert.DoesNotContain("Venue confirmed", e.Text, StringComparison.Ordinal));

        // P4_US5_AS4, AS6: Omar ("All") also hears about both comments; Farah ("None") gets nothing.
        var omarEmails = await EmailsAsync("omar.p4u5@example.com", 2);
        Assert.Equal(2, omarEmails.Count(e => e.Text.Contains("Amina Mail commented on EMW-1 Plan the launch.", StringComparison.Ordinal)));
        Assert.Empty(await App.EmailsToAsync("farah.p4u5@example.com"));
        Assert.Equal(2, (await App.EmailsToAsync("bilal.p4u5@example.com")).Count); // no email for the plain comment

        // P4_US5_AS10: the link opens the item after signing in.
        var link = bilalEmails[0].Text.Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("Open it: ", StringComparison.Ordinal))["Open it: ".Length..];
        var bilal = await NewPageAsync();
        await bilal.GotoAsync(link);
        await bilal.GetByLabel("User name").FillAsync("bilal.p4u5");
        await bilal.GetByLabel("Password").FillAsync(bilalPassword);
        await bilal.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await WaitForPathAsync(bilal, "/spaces/EMW/board?task=EMW-1");
        await Assertions.Expect(bilal.GetByTestId("drawer-key")).ToHaveTextAsync("EMW-1");
    }
}
