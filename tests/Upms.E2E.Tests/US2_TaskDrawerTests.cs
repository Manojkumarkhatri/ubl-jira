using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>User Story 2 end to end: work on a task in the details drawer.</summary>
public sealed partial class US2_TaskDrawerTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static async Task CreateProjectAsync(IPage page, string name, string key)
    {
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync(name);
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await page.WaitForURLAsync($"**/projects/{key}/board");
        await WaitForInteractivityAsync(page);
    }

    private static async Task AddTaskAsync(IPage page, string title)
    {
        var box = Column(page, 0).GetByPlaceholder("What needs to be done?");
        await box.FillAsync(title);
        await box.PressAsync("Enter");
        await Assertions.Expect(Column(page, 0).Locator(".card-title", new() { HasText = title })).ToBeVisibleAsync();
    }

    private static async Task OpenCardAsync(IPage page, string key)
    {
        await page.GetByTestId($"card-{key}").Locator(".card-title").ClickAsync();
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-key")).ToHaveTextAsync(key);
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-title")).ToBeVisibleAsync();
    }

    private static async Task CloseDrawerAsync(IPage page)
    {
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Drawer(page)).ToBeHiddenAsync();
        await Assertions.Expect(page).Not.ToHaveURLAsync(TaskInUrl());
    }

    [Fact]
    public async Task US2_Independent_test_edit_a_task_in_the_drawer_and_everything_is_saved()
    {
        var carlaPassword = await App.CreateUserAsync("carla", "Carla Diaz");
        var daniyalPassword = await App.CreateUserAsync("daniyal", "Daniyal Raza");
        var page = await SignInAsync("carla", carlaPassword);
        await CreateProjectAsync(page, "Mobile App", "MOB");
        await AddTaskAsync(page, "Design the login screen");

        // US2_AS1: selecting the card opens its drawer beside the board, and focus moves into it.
        await OpenCardAsync(page, "MOB-1");
        await Assertions.Expect(page).ToHaveURLAsync(TaskInUrl());
        await Assertions.Expect(page.Locator("#drawer-heading")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("h1", new() { HasText = "Mobile App" })).ToBeVisibleAsync(); // the board stays visible
        await Assertions.Expect(Drawer(page).Locator("#drawer-status option:checked")).ToHaveTextAsync("To Do");
        await Assertions.Expect(Drawer(page).Locator("#drawer-priority")).ToHaveValueAsync("Medium");
        await page.AssertNoAccessibilityViolationsAsync();

        // US2_AS2: a new title is saved, confirmed and shown on the card.
        await Drawer(page).GetByTestId("drawer-title").ClickAsync();
        await Drawer(page).Locator("#drawer-title-input").FillAsync("Design the sign-in screen");
        await Drawer(page).Locator("#drawer-title-input").PressAsync("Enter");
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-saved")).ToContainTextAsync("Title saved");
        await Assertions.Expect(page.GetByTestId("card-MOB-1")).ToContainTextAsync("Design the sign-in screen");

        // US2_AS3: line breaks are kept and web links open in a new tab.
        await Drawer(page).GetByTestId("edit-description").ClickAsync();
        await Drawer(page).Locator("#drawer-description-input").FillAsync("Two fields and a button.\nSee https://example.com/spec for the flow.");
        await Drawer(page).GetByTestId("save-description").ClickAsync();
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-saved")).ToContainTextAsync("Description saved");
        var description = Drawer(page).GetByTestId("drawer-description").Locator(".plain-text");
        await Assertions.Expect(description).ToHaveTextAsync("Two fields and a button.\nSee https://example.com/spec for the flow.");
        await Assertions.Expect(description.GetByRole(AriaRole.Link)).ToHaveAttributeAsync("href", "https://example.com/spec");
        await Assertions.Expect(description.GetByRole(AriaRole.Link)).ToHaveAttributeAsync("target", "_blank");

        // US2_AS4: priority High shows on the card.
        await Drawer(page).Locator("#drawer-priority").SelectOptionAsync("High");
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-saved")).ToContainTextAsync("Priority changed to High");
        await Assertions.Expect(page.GetByTestId("card-MOB-1").Locator(".pri-high")).ToHaveCountAsync(1);

        // US2_AS6: three sub-tasks get their own keys; completing one shows "1/3" on the card.
        foreach (var title in new[] { "Field layout", "Error messages", "Forgot-password link" })
        {
            await Drawer(page).Locator("#add-subtask").FillAsync(title);
            await Drawer(page).Locator("#add-subtask").PressAsync("Enter");
            await Assertions.Expect(Drawer(page).GetByTestId("subtask").Filter(new() { HasText = title })).ToBeVisibleAsync();
        }

        await Assertions.Expect(Drawer(page).GetByTestId("subtask").Locator(".key")).ToHaveTextAsync(["MOB-2", "MOB-3", "MOB-4"]);
        await Drawer(page).GetByTestId("subtask").First.GetByTestId("mark-done").ClickAsync();
        await Assertions.Expect(Drawer(page).GetByText("1 of 3 done")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("card-MOB-1").GetByTestId("subtask-progress")).ToContainTextAsync("1/3");

        // US2_AS8: two comments, one edited.
        foreach (var body in new[] { "Keep the logo small.", "Use the brand blue for the button." })
        {
            await Drawer(page).Locator("#new-comment").FillAsync(body);
            await Drawer(page).GetByTestId("post-comment").ClickAsync();
            await Assertions.Expect(Drawer(page).GetByTestId("comment").Filter(new() { HasText = body })).ToBeVisibleAsync();
        }

        var first = Drawer(page).GetByTestId("comment").First;
        await first.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Drawer(page).GetByLabel("Edit your comment").FillAsync("Keep the logo small and centred.");
        await Drawer(page).GetByTestId("save-comment").ClickAsync();
        await Assertions.Expect(first).ToContainTextAsync("Keep the logo small and centred.");
        await Assertions.Expect(first).ToContainTextAsync("(edited)");
        await Assertions.Expect(first).ToContainTextAsync("Carla Diaz");

        // Close and reopen: everything was saved, and focus returned to the card.
        await CloseDrawerAsync(page);
        await Assertions.Expect(page.Locator("#card-MOB-1-title")).ToBeFocusedAsync();
        await OpenCardAsync(page, "MOB-1");
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-title")).ToHaveTextAsync("Design the sign-in screen");
        await Assertions.Expect(Drawer(page).Locator("#drawer-priority")).ToHaveValueAsync("High");
        await Assertions.Expect(Drawer(page).GetByTestId("subtask")).ToHaveCountAsync(3);
        await Assertions.Expect(Drawer(page).GetByTestId("comment")).ToHaveCountAsync(2);

        // US2_AS9: the history lists each change in time order with who, what, and old and new values.
        await Drawer(page).Locator("summary", new() { HasText = "History" }).ClickAsync();
        var history = Drawer(page).GetByTestId("history-entry");
        await Assertions.Expect(history).ToHaveCountAsync(10);
        await Assertions.Expect(history.Nth(0)).ToContainTextAsync("Carla Diaz created the task in To Do");
        await Assertions.Expect(history.Nth(1)).ToContainTextAsync("changed the title from “Design the login screen” to “Design the sign-in screen”");
        await Assertions.Expect(history.Nth(2)).ToContainTextAsync("added a description");
        await Assertions.Expect(history.Nth(3)).ToContainTextAsync("changed the priority from Medium to High");
        await Assertions.Expect(history.Nth(4)).ToContainTextAsync("added sub-task MOB-2 “Field layout”");
        await Assertions.Expect(history.Nth(9)).ToContainTextAsync("edited a comment");
        await page.AssertNoAccessibilityViolationsAsync();

        // US2_AS7: a sub-task opens in the same drawer with a link back; sub-tasks are never board cards.
        await Drawer(page).GetByRole(AriaRole.Link, new() { Name = "MOB-3 Error messages" }).ClickAsync();
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-key")).ToHaveTextAsync("MOB-3");
        await Assertions.Expect(page.Locator("#drawer-heading")).ToBeFocusedAsync();
        await Drawer(page).GetByRole(AriaRole.Link, new() { Name = "MOB-1 Design the sign-in screen" }).ClickAsync();
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-key")).ToHaveTextAsync("MOB-1");
        await Assertions.Expect(page.Locator("[data-testid^='card-MOB-']")).ToHaveCountAsync(1);

        // US2_AS5: Done moves the card to the Done column, with a warning about the open sub-tasks.
        await Drawer(page).Locator("#drawer-status").SelectOptionAsync("Done");
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-warning")).ToContainTextAsync("2 sub-tasks are still open: MOB-3, MOB-4");
        await Assertions.Expect(Column(page, 2).GetByTestId("card-MOB-1")).ToBeVisibleAsync();

        // FR-023: another person opens the task from its link.
        var daniyal = await SignInAsync("daniyal", daniyalPassword);
        await GotoAsync(daniyal, "/projects/MOB/board?task=MOB-1");
        await Assertions.Expect(Drawer(daniyal).GetByTestId("drawer-title")).ToHaveTextAsync("Design the sign-in screen");
        await Assertions.Expect(Drawer(daniyal).GetByTestId("comment")).ToHaveCountAsync(2);
        // US2_AS8: nobody can edit or delete another person's comment.
        await Assertions.Expect(Drawer(daniyal).GetByTestId("comment").GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task US2_AS10_When_two_people_edit_the_description_the_second_sees_the_latest_and_keeps_their_text()
    {
        var karimPassword = await App.CreateUserAsync("karim", "Karim Baig");
        var lailaPassword = await App.CreateUserAsync("laila", "Laila Noor");
        var karim = await SignInAsync("karim", karimPassword);
        await CreateProjectAsync(karim, "Branch Network", "BRN");
        await AddTaskAsync(karim, "Survey the branches");
        var laila = await SignInAsync("laila", lailaPassword);
        await GotoAsync(laila, "/projects/BRN/board?task=BRN-1");
        await Assertions.Expect(Drawer(laila).GetByTestId("drawer-title")).ToBeVisibleAsync();

        await OpenCardAsync(karim, "BRN-1");
        await Drawer(karim).GetByTestId("edit-description").ClickAsync();
        await Drawer(karim).Locator("#drawer-description-input").FillAsync("Karim's plan: visit ten branches.");
        await Drawer(karim).GetByTestId("save-description").ClickAsync();
        await Assertions.Expect(Drawer(karim).GetByTestId("drawer-saved")).ToContainTextAsync("Description saved");

        await Drawer(laila).GetByTestId("edit-description").ClickAsync();
        await Drawer(laila).Locator("#drawer-description-input").FillAsync("Laila's plan: send a questionnaire.");
        await Drawer(laila).GetByTestId("save-description").ClickAsync();

        var conflict = Drawer(laila).GetByTestId("conflict");
        await Assertions.Expect(conflict).ToContainTextAsync("was changed by someone else");
        await Assertions.Expect(conflict).ToContainTextAsync("Karim's plan: visit ten branches.");
        await Assertions.Expect(Drawer(laila).Locator("#drawer-description-input")).ToHaveValueAsync("Laila's plan: send a questionnaire.");

        // Saving again, after seeing the latest version, keeps Laila's text.
        await Drawer(laila).GetByTestId("save-description").ClickAsync();
        await Assertions.Expect(Drawer(laila).GetByTestId("drawer-saved")).ToContainTextAsync("Description saved");
        await Assertions.Expect(Drawer(laila).GetByTestId("drawer-description")).ToContainTextAsync("Laila's plan");
    }

    [Fact]
    public async Task US2_AS11_A_deleted_task_leaves_the_board_and_an_administrator_restores_it()
    {
        var omarPassword = await App.CreateUserAsync("omar", "Omar Farooq");
        var omar = await SignInAsync("omar", omarPassword);
        await CreateProjectAsync(omar, "Card Services", "CRD");
        await AddTaskAsync(omar, "Replace the card printer");
        await OpenCardAsync(omar, "CRD-1");
        await Drawer(omar).Locator("#add-subtask").FillAsync("Get three quotes");
        await Drawer(omar).Locator("#add-subtask").PressAsync("Enter");
        await Assertions.Expect(Drawer(omar).GetByTestId("subtask")).ToHaveCountAsync(1);

        await Drawer(omar).GetByTestId("delete-task").ClickAsync();
        var confirm = omar.GetByTestId("delete-confirm");
        await Assertions.Expect(confirm).ToContainTextAsync("Its 1 sub-task will be deleted too.");
        await omar.AssertNoAccessibilityViolationsAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Delete task" }).ClickAsync();

        await Assertions.Expect(Drawer(omar)).ToBeHiddenAsync();
        await Assertions.Expect(omar.GetByTestId("card-CRD-1")).ToHaveCountAsync(0);
        await Assertions.Expect(omar.GetByRole(AriaRole.Link, new() { Name = "Deleted tasks" })).ToHaveCountAsync(0);

        var admin = await SignInAsync(AppFixture.AdminUserName, AppFixture.AdminPassword);
        await GotoAsync(admin, "/projects/CRD/board");
        await admin.GetByRole(AriaRole.Link, new() { Name = "Deleted tasks" }).ClickAsync();
        await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "Deleted tasks" })).ToBeVisibleAsync();
        var row = admin.GetByTestId("deleted-CRD-1");
        await Assertions.Expect(row).ToContainTextAsync("Replace the card printer");
        await Assertions.Expect(row).ToContainTextAsync("Omar Farooq");
        await admin.AssertNoAccessibilityViolationsAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Restore CRD-1" }).ClickAsync();
        await Assertions.Expect(admin.GetByTestId("restored")).ToContainTextAsync("Restored CRD-1");

        await GotoAsync(omar, "/projects/CRD/board");
        await Assertions.Expect(omar.GetByTestId("card-CRD-1").GetByTestId("subtask-progress")).ToContainTextAsync("0/1");
    }

    [GeneratedRegex(@"[?&]task=")]
    private static partial Regex TaskInUrl();
}
