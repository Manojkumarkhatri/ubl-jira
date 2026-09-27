using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Every Phase 1 screen works at 360 px wide, and each story's main path works with the keyboard alone
/// (FR-042, SC-008). Keyboard steps reach controls with Tab, so each control is also proven to be in the tab order.</summary>
public sealed class ResponsiveAndKeyboardTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    /// <summary>Presses Tab (or Shift+Tab) until <paramref name="target"/> has focus.</summary>
    private static async Task TabToAsync(IPage page, ILocator target, int maxPresses = 80, bool backwards = false)
    {
        var handle = await target.ElementHandleAsync();
        for (var i = 0; i < maxPresses; i++)
        {
            await page.Keyboard.PressAsync(backwards ? "Shift+Tab" : "Tab");
            if (await page.EvaluateAsync<bool>("el => el === document.activeElement", handle))
            {
                return;
            }
        }

        Assert.Fail($"{target} was not reached with {(backwards ? "Shift+Tab" : "Tab")} in {maxPresses} presses.");
    }

    private static async Task AssertNoSidewaysScrollAsync(IPage page)
    {
        var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
        Assert.True(overflow <= 0, $"{page.Url} is {overflow}px wider than the screen.");
    }

    private async Task<IPage> SignInWithKeyboardAsync(string userName, string password, int width = 1280)
    {
        var page = await NewPageAsync(width, 900);
        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("User name").FocusAsync();
        await page.Keyboard.TypeAsync(userName);
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync(password);
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.Ordinal));
        await WaitForInteractivityAsync(page);
        return page;
    }

    [Fact]
    public async Task The_project_list_board_drawer_and_column_settings_work_at_360_px()
    {
        var password = await App.CreateUserAsync("vera", "Vera Lobo");
        var desk = await SignInAsync("vera", password);
        await GotoAsync(desk, "/projects");
        await desk.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = desk.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Small Screens");
        await dialog.GetByLabel("Key").FillAsync("SML");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await desk.WaitForURLAsync("**/projects/SML/board");
        await WaitForInteractivityAsync(desk);
        var box = Column(desk, 0).GetByPlaceholder("What needs to be done?");
        await box.FillAsync("A task with a fairly long title that has to wrap on a phone screen");
        await box.PressAsync("Enter");
        await Assertions.Expect(desk.GetByTestId("card-SML-1")).ToBeVisibleAsync();

        var phone = await SignInAsync("vera", password);
        await phone.SetViewportSizeAsync(360, 740);

        await GotoAsync(phone, "/projects");
        await Assertions.Expect(phone.GetByRole(AriaRole.Link, new() { Name = "Small Screens" })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/projects/SML/board");
        await Assertions.Expect(phone.GetByTestId("card-SML-1")).ToBeInViewportAsync();
        await AssertNoSidewaysScrollAsync(phone); // the columns scroll inside the board, not the page
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/projects/SML/board?task=SML-1");
        var drawer = phone.GetByTestId("drawer");
        await Assertions.Expect(drawer.GetByTestId("drawer-title")).ToBeInViewportAsync();
        var drawerWidth = await drawer.EvaluateAsync<double>("el => el.getBoundingClientRect().width");
        Assert.InRange(drawerWidth, 350, 360);
        await Assertions.Expect(drawer.Locator("#drawer-status")).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/projects/SML/settings");
        await Assertions.Expect(phone.GetByTestId("column-row").First.GetByRole(AriaRole.Button, new() { Name = "Move right" })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task US1_Main_path_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("wasim", "Wasim Akram");
        var page = await SignInWithKeyboardAsync("wasim", password);
        await GotoAsync(page, "/projects");

        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First);
        await page.Keyboard.PressAsync("Enter");
        var dialog = page.GetByTestId("create-project");
        await Assertions.Expect(dialog.GetByLabel("Name")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Keyboard Board");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(dialog.GetByLabel("Key")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("KBD");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForURLAsync("**/projects/KBD/board");
        await WaitForInteractivityAsync(page);

        await TabToAsync(page, Column(page, 0).GetByPlaceholder("What needs to be done?"));
        foreach (var title in new[] { "Plan the sprint", "Write the brief" })
        {
            await page.Keyboard.TypeAsync(title);
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(Column(page, 0).Locator(".card-title", new() { HasText = title })).ToBeVisibleAsync();
        }

        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Move KBD-1 to…" }), backwards: true);
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.GetByTestId("card-KBD-1").GetByRole(AriaRole.Button, new() { Name = "In Progress: bottom" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Column(page, 1).Locator(".card-title")).ToHaveTextAsync(["Plan the sprint"]);
        await Assertions.Expect(page.Locator("#card-KBD-1-title")).ToBeFocusedAsync(); // focus follows the card
    }

    [Fact]
    public async Task US2_Main_path_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("xenia", "Xenia Shah");
        var page = await SignInWithKeyboardAsync("xenia", password);
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Keyboard Drawer");
        await dialog.GetByLabel("Key").FillAsync("KDR");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await page.WaitForURLAsync("**/projects/KDR/board");
        await WaitForInteractivityAsync(page);
        var box = Column(page, 0).GetByPlaceholder("What needs to be done?");
        await box.FillAsync("Draft the letter");
        await box.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("card-KDR-1")).ToBeVisibleAsync();

        await TabToAsync(page, page.Locator("#card-KDR-1-title"), backwards: true);
        await page.Keyboard.PressAsync("Enter");
        var drawer = page.GetByTestId("drawer");
        await Assertions.Expect(page.Locator("#drawer-heading")).ToBeFocusedAsync();

        await TabToAsync(page, drawer.GetByTestId("drawer-title"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.Locator("#drawer-title-input")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("Draft the customer letter");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("drawer-saved")).ToContainTextAsync("Title saved");

        await TabToAsync(page, drawer.Locator("#add-subtask"));
        await page.Keyboard.TypeAsync("Check the address");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("subtask")).ToHaveCountAsync(1);

        await TabToAsync(page, drawer.Locator("#new-comment"));
        await page.Keyboard.TypeAsync("Ready for review.");
        await TabToAsync(page, drawer.GetByTestId("post-comment"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("comment")).ToHaveCountAsync(1);

        await TabToAsync(page, drawer.Locator("#drawer-priority"));
        await page.Keyboard.PressAsync("ArrowUp"); // Medium → High
        await Assertions.Expect(drawer.GetByTestId("drawer-saved")).ToContainTextAsync("Priority changed to High");

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#card-KDR-1-title")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByTestId("card-KDR-1")).ToContainTextAsync("Draft the customer letter");
    }

    [Fact]
    public async Task US3_Main_path_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("yusuf", "Yusuf Ali");
        var page = await SignInWithKeyboardAsync("yusuf", password);
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Keyboard Columns");
        await dialog.GetByLabel("Key").FillAsync("KCL");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await page.WaitForURLAsync("**/projects/KCL/board");
        await GotoAsync(page, "/projects/KCL/settings");

        await TabToAsync(page, page.Locator("#new-column-name"), maxPresses: 120);
        await page.Keyboard.TypeAsync("Testing");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("columns-saved")).ToContainTextAsync("Added the column Testing");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["To Do", "In Progress", "Testing", "Done"]);

        var testing = page.GetByTestId("column-row").Filter(new() { Has = page.Locator(".column-name", new() { HasText = "Testing" }) });
        await TabToAsync(page, testing.GetByRole(AriaRole.Button, new() { Name = "Move left" }), backwards: true);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["To Do", "Testing", "In Progress", "Done"]);
        await Assertions.Expect(testing.GetByRole(AriaRole.Button, new() { Name = "Move left" })).ToBeFocusedAsync();

        await TabToAsync(page, testing.GetByRole(AriaRole.Button, new() { Name = "Rename" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByLabel("New name for Testing")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("QA");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator(".column-name")).ToHaveTextAsync(["To Do", "QA", "In Progress", "Done"]);
        await Assertions.Expect(page.GetByTestId("column-row").Nth(1).GetByRole(AriaRole.Button, new() { Name = "Rename" })).ToBeFocusedAsync();
    }
}
