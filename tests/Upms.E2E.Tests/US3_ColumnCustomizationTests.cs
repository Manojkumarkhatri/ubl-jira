using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>User Story 3 end to end: the project owner shapes the board's columns.</summary>
public sealed class US3_ColumnCustomizationTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    private static ILocator ColumnRow(IPage page, string name) =>
        page.GetByTestId("column-row").Filter(new() { Has = page.Locator(".column-name", new() { HasText = name }) });

    private static async Task AddTaskAsync(IPage page, int column, string title)
    {
        var box = Column(page, column).GetByPlaceholder("What needs to be done?");
        await box.FillAsync(title);
        await box.PressAsync("Enter");
        await Assertions.Expect(Column(page, column).Locator(".card-title", new() { HasText = title })).ToBeVisibleAsync();
    }

    private static async Task ExpectSavedAsync(IPage page, string text) =>
        await Assertions.Expect(page.GetByTestId("columns-saved")).ToContainTextAsync(text);

    [Fact]
    public async Task US3_Independent_test_the_owner_customizes_the_columns_and_no_task_is_lost()
    {
        var rafaelPassword = await App.CreateUserAsync("rafael", "Rafael Costa");
        var sanaPassword = await App.CreateUserAsync("sana", "Sana Mirza");
        var page = await SignInAsync("rafael", rafaelPassword);
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Treasury Operations");
        await dialog.GetByLabel("Key").FillAsync("TRS");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await page.WaitForURLAsync("**/projects/TRS/board");
        await WaitForInteractivityAsync(page);
        foreach (var title in new[] { "Reconcile nostro", "Review limits", "Update the cash forecast" })
        {
            await AddTaskAsync(page, 1, title);
        }

        await AddTaskAsync(page, 0, "Renew the FX licence");

        // US3_AS1: add "In Review" (in progress) between In Progress and Done.
        await page.GetByRole(AriaRole.Link, new() { Name = "Project settings" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Columns" })).ToBeVisibleAsync();
        await page.Locator("#new-column-name").FillAsync("In Review");
        await Assertions.Expect(page.Locator("#new-column-type")).ToHaveValueAsync("InProgress");
        await Assertions.Expect(page.Locator("#new-column-position option:checked")).ToHaveTextAsync("After In Progress");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add column" }).ClickAsync();
        await ExpectSavedAsync(page, "Added the column In Review");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["To Do", "In Progress", "In Review", "Done"]);

        // US3_AS2: rename To Do to Backlog.
        await ColumnRow(page, "To Do").GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await page.GetByLabel("New name for To Do").FillAsync("Backlog");
        await page.GetByLabel("New name for To Do").PressAsync("Enter");
        await ExpectSavedAsync(page, "Renamed To Do to Backlog");

        // A limit of 3 on In Progress.
        await ColumnRow(page, "In Progress").GetByLabel("Limit").FillAsync("3");
        await ColumnRow(page, "In Progress").GetByLabel("Limit").PressAsync("Enter");
        await ExpectSavedAsync(page, "In Progress now has a limit of 3");

        // US3_AS3: move a column with the keyboard, then back by dragging it.
        var moveLeft = ColumnRow(page, "In Review").GetByRole(AriaRole.Button, new() { Name = "Move left" });
        await moveLeft.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["Backlog", "In Review", "In Progress", "Done"]);
        await ColumnRow(page, "In Review").Locator(".drag-handle").DragToAsync(ColumnRow(page, "In Progress").Locator(".drag-handle"));
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["Backlog", "In Progress", "In Review", "Done"]);
        await page.AssertNoAccessibilityViolationsAsync();

        // The board shows the new columns for everyone; US3_AS4: a fourth card marks In Progress over its limit.
        await GotoAsync(page, "/projects/TRS/board");
        await Assertions.Expect(page.GetByTestId("column").Locator("h2")).ToHaveTextAsync(["Backlog", "In Progress", "In Review", "Done"]);
        await Assertions.Expect(Column(page, 1).Locator(".count")).ToHaveTextAsync("3 of 3");
        await page.GetByTestId("card-TRS-4").DragToAsync(Column(page, 1).GetByTestId("column-drop-end"));
        await Assertions.Expect(Column(page, 1).Locator(".count")).ToHaveTextAsync("4 of 3");
        await Assertions.Expect(Column(page, 1).GetByText("Over limit")).ToBeVisibleAsync();
        await AddTaskAsync(page, 2, "Check the settlement file");
        await AddTaskAsync(page, 2, "Sign off the forecast");

        // US3_AS5: delete In Review and send its tasks to Done; no task is lost.
        await GotoAsync(page, "/projects/TRS/settings");
        await ColumnRow(page, "In Review").GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        var confirm = page.GetByTestId("delete-column");
        await confirm.GetByLabel("Move its work items (2) to").SelectOptionAsync("Done");
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Delete column" }).ClickAsync();
        await ExpectSavedAsync(page, "its work items moved to Done");
        await GotoAsync(page, "/projects/TRS/board");
        await Assertions.Expect(page.GetByTestId("column").Locator("h2")).ToHaveTextAsync(["Backlog", "In Progress", "Done"]);
        await Assertions.Expect(Column(page, 2).Locator(".card-title")).ToHaveTextAsync(["Check the settlement file", "Sign off the forecast"]);
        await Assertions.Expect(page.Locator("[data-testid^='card-TRS-']")).ToHaveCountAsync(6);

        // US3_AS8: someone who is not the owner cannot change the columns, but still works on tasks.
        var sana = await SignInAsync("sana", sanaPassword);
        await GotoAsync(sana, "/projects/TRS/board");
        await Assertions.Expect(sana.GetByRole(AriaRole.Link, new() { Name = "Project settings" })).ToHaveCountAsync(0);
        await AddTaskAsync(sana, 0, "Sana's task");
        await sana.GotoAsync("/projects/TRS/settings");
        await Assertions.Expect(sana.GetByRole(AriaRole.Heading, new() { Name = "Not found" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task US3_AS6_The_last_done_column_cannot_be_deleted()
    {
        var password = await App.CreateUserAsync("tariq", "Tariq Jamil");
        var page = await SignInAsync("tariq", password);
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Audit Findings");
        await dialog.GetByLabel("Key").FillAsync("AUD");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await page.WaitForURLAsync("**/projects/AUD/board");
        await GotoAsync(page, "/projects/AUD/settings");

        await ColumnRow(page, "Done").GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await page.GetByTestId("delete-column").GetByRole(AriaRole.Button, new() { Name = "Delete column" }).ClickAsync();

        await Assertions.Expect(page.GetByTestId("delete-column")).ToContainTextAsync("only \"done\" column");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["To Do", "In Progress", "Done"]);
    }
}
