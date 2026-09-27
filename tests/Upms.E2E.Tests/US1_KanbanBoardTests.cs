using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>User Story 1 end to end: create a Kanban project and track tasks on its board.</summary>
public sealed partial class US1_KanbanBoardTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    private static ILocator Titles(IPage page, int column) => Column(page, column).Locator(".card-title");

    private static async Task AddTaskAsync(IPage page, int column, string title)
    {
        var box = Column(page, column).GetByPlaceholder("What needs to be done?");
        await box.FillAsync(title);
        await box.PressAsync("Enter");
        await Assertions.Expect(Column(page, column).Locator(".card-title", new() { HasText = title })).ToBeVisibleAsync();
        await Assertions.Expect(box).ToHaveValueAsync("");
    }

    [Fact]
    public async Task US1_AS1_Visitors_who_are_not_signed_in_are_sent_to_sign_in()
    {
        var page = await NewPageAsync();

        await page.GotoAsync("/projects");

        await Assertions.Expect(page).ToHaveURLAsync(LoginUrl());
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task US1_AS2_A_temporary_password_must_be_replaced_first()
    {
        var temporary = await App.CreateUserAsync("ines", "Ines Qureshi", mustChangePassword: true);

        var page = await SignInAsync("ines", temporary);
        await page.GotoAsync("/projects");

        await Assertions.Expect(page).ToHaveURLAsync(ChangePasswordUrl());
    }

    [Fact]
    public async Task US1_Independent_test_create_a_project_add_tasks_move_them_and_everyone_sees_the_same_board()
    {
        var aminaPassword = await App.CreateUserAsync("amina", "Amina Khan");
        var bilalPassword = await App.CreateUserAsync("bilal", "Bilal Ahmed");
        var page = await SignInAsync("amina", aminaPassword);

        // US1_AS3: create "Website Revamp"; the key is suggested, then changed to WEB.
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Website Revamp");
        await Assertions.Expect(dialog.GetByLabel("Key")).ToHaveValueAsync("WR");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("WEB");
        await dialog.GetByLabel("Description (optional)").FillAsync("The new public website");
        await page.AssertNoAccessibilityViolationsAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(BoardUrl());
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Website Revamp" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("column").Locator("h2")).ToHaveTextAsync(["To Do", "In Progress", "Done"]);

        // US1_AS5: five tasks typed straight into the columns.
        await AddTaskAsync(page, 0, "Design the home page");
        await AddTaskAsync(page, 0, "Write the copy");
        await AddTaskAsync(page, 0, "Pick the colours");
        await AddTaskAsync(page, 1, "Set up hosting");
        await AddTaskAsync(page, 0, "Plan the launch");
        await Assertions.Expect(Titles(page, 0)).ToHaveTextAsync(["Design the home page", "Write the copy", "Pick the colours", "Plan the launch"]);
        await page.AssertNoAccessibilityViolationsAsync();

        // US1_AS6: drag two cards to other columns.
        await page.GetByTestId("card-WEB-1").DragToAsync(Column(page, 1).GetByTestId("column-drop-end"));
        await Assertions.Expect(Titles(page, 1)).ToHaveTextAsync(["Set up hosting", "Design the home page"]);
        await page.GetByTestId("card-WEB-4").DragToAsync(Column(page, 2).GetByTestId("column-drop-end"));
        await Assertions.Expect(Titles(page, 2)).ToHaveTextAsync(["Set up hosting"]);

        // US1_AS7: reorder a column by dropping a card above another.
        await page.GetByTestId("card-WEB-5").DragToAsync(page.GetByTestId("card-WEB-2"));
        await Assertions.Expect(Titles(page, 0)).ToHaveTextAsync(["Plan the launch", "Write the copy", "Pick the colours"]);

        // US1_AS8: move a card with the keyboard only.
        var moveButton = page.GetByTestId("card-WEB-3").GetByRole(AriaRole.Button, new() { Name = "Move WEB-3 to…" });
        await moveButton.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        var target = page.GetByTestId("card-WEB-3").GetByRole(AriaRole.Button, new() { Name = "Done: bottom" });
        await target.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Titles(page, 2)).ToHaveTextAsync(["Set up hosting", "Pick the colours"]);

        // After reloading, the board is exactly as it was left.
        await page.ReloadAsync();
        await WaitForInteractivityAsync(page);
        await Assertions.Expect(Titles(page, 0)).ToHaveTextAsync(["Plan the launch", "Write the copy"]);
        await Assertions.Expect(Titles(page, 1)).ToHaveTextAsync(["Design the home page"]);
        await Assertions.Expect(Titles(page, 2)).ToHaveTextAsync(["Set up hosting", "Pick the colours"]);

        // Another member of the project sees the same board.
        await App.AddMemberAsync("WEB", "bilal");
        var bilal = await SignInAsync("bilal", bilalPassword);
        await GotoAsync(bilal, "/projects/WEB/board");
        await Assertions.Expect(Titles(bilal, 0)).ToHaveTextAsync(["Plan the launch", "Write the copy"]);
        await Assertions.Expect(Titles(bilal, 1)).ToHaveTextAsync(["Design the home page"]);
        await Assertions.Expect(Titles(bilal, 2)).ToHaveTextAsync(["Set up hosting", "Pick the colours"]);

        await GotoAsync(bilal, "/projects");
        await Assertions.Expect(bilal.GetByRole(AriaRole.Link, new() { Name = "Website Revamp" })).ToBeVisibleAsync();
        await bilal.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task US1_AS4_A_key_already_in_use_is_refused_and_the_input_is_kept()
    {
        var password = await App.CreateUserAsync("jamal", "Jamal Siddiqui");
        var page = await SignInAsync("jamal", password);
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Payroll");
        await Assertions.Expect(dialog.GetByLabel("Key")).ToHaveValueAsync("PAY");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(PayBoardUrl());

        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Payments");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("PAY");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();

        await Assertions.Expect(dialog.GetByText("The key PAY is already used by another project.")).ToBeVisibleAsync();
        await Assertions.Expect(dialog.GetByLabel("Name")).ToHaveValueAsync("Payments");
        await Assertions.Expect(dialog.GetByLabel("Key")).ToHaveValueAsync("PAY");
    }

    [GeneratedRegex("/Account/Login")]
    private static partial Regex LoginUrl();

    [GeneratedRegex("/Account/ChangePassword")]
    private static partial Regex ChangePasswordUrl();

    [GeneratedRegex("/projects/WEB/board$")]
    private static partial Regex BoardUrl();

    [GeneratedRegex("/projects/PAY/board$")]
    private static partial Regex PayBoardUrl();
}
