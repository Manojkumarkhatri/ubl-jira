using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Every Phase 1, 2 and 3 screen works at 360 px wide, and each story's main path works with the keyboard
/// alone (Phase 1 FR-042, SC-008; Phase 2 FR-041, SC-009; Phase 3 FR-056, SC-011). Keyboard steps reach controls with
/// Tab, so each control is also proven to be in the tab order.</summary>
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

    private static string Long(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Creates a project in the browser, waiting for the suggested key before typing over it.</summary>
    private static async Task CreateProjectAsync(IPage page, string name, string key, string? template = null)
    {
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        if (template is not null)
        {
            await dialog.GetByLabel(template, new() { Exact = true }).CheckAsync();
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync(name);
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, $"/spaces/{key}/board");
        await WaitForInteractivityAsync(page);
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
        await GotoAsync(desk, "/spaces");
        await desk.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = desk.GetByTestId("create-project");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync("Small Screens");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("SML");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(desk, "/spaces/SML/board");
        await WaitForInteractivityAsync(desk);
        var box = Column(desk, 0).GetByPlaceholder("What needs to be done?");
        await box.FillAsync("A task with a fairly long title that has to wrap on a phone screen");
        await box.PressAsync("Enter");
        await Assertions.Expect(desk.GetByTestId("card-SML-1")).ToBeVisibleAsync();

        var phone = await SignInAsync("vera", password);
        await phone.SetViewportSizeAsync(360, 740);

        await GotoAsync(phone, "/spaces");
        await Assertions.Expect(phone.GetByRole(AriaRole.Link, new() { Name = "Small Screens" })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/spaces/SML/board");
        await Assertions.Expect(phone.GetByTestId("card-SML-1")).ToBeInViewportAsync();
        await AssertNoSidewaysScrollAsync(phone); // the columns scroll inside the board, not the page
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/spaces/SML/board?task=SML-1");
        var drawer = phone.GetByTestId("drawer");
        await Assertions.Expect(drawer.GetByTestId("drawer-title")).ToBeInViewportAsync();
        var drawerWidth = await drawer.EvaluateAsync<double>("el => el.getBoundingClientRect().width");
        Assert.InRange(drawerWidth, 350, 360);
        await Assertions.Expect(drawer.Locator("#drawer-status")).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(phone, "/spaces/SML/settings");
        await Assertions.Expect(phone.GetByTestId("column-row").First.GetByRole(AriaRole.Button, new() { Name = "Move right" })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task JP_Main_paths_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("kamran.jp", "Kamran Keys");
        var page = await SignInWithKeyboardAsync("kamran.jp", password);
        await CreateProjectAsync(page, "Keyboard Lanes", "KBL");
        await App.AddPlanAsync("KBL",
        [
            (Domain.Work.WorkItemType.Epic, "Card refunds", null, null, null),       // KBL-1
            (Domain.Work.WorkItemType.Story, "Refund to card", "KBL-1", null, null), // KBL-2
        ]);
        await GotoAsync(page, "/spaces/KBL/board");

        // The Create form from the header (Jira parity pass FR-001, FR-028).
        await TabToAsync(page, page.GetByTestId("create-button"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#create-summary")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Write the refund API");
        await TabToAsync(page, page.GetByTestId("create-submit"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("card-KBL-3")).ToBeVisibleAsync();

        // A column's menu (FR-011): it opens on its first action and Escape gives focus back.
        var columnMenu = page.GetByRole(AriaRole.Button, new() { Name = "Column actions for In Progress" });
        await TabToAsync(page, columnMenu);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByRole(AriaRole.Menuitem).First).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(columnMenu).ToBeFocusedAsync();

        // Lanes by epic, and "Move to › Lane" (FR-026, FR-027).
        await TabToAsync(page, page.Locator("#board-group"), backwards: true);
        await page.Keyboard.PressAsync("ArrowDown"); // None → Epic
        await WaitForPathAsync(page, "/spaces/KBL/board?group=epic");
        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Actions for KBL-3" }));
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.GetByTestId("card-KBL-3").GetByRole(AriaRole.Button, new() { Name = "Lane: KBL-1 Card refunds" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("lane").First.GetByTestId("card-KBL-3")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#card-KBL-3-title")).ToBeFocusedAsync(); // focus follows the card
        var laneHeader = page.GetByTestId("lane").First.Locator(".lane-head button");
        await TabToAsync(page, laneHeader, backwards: true);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(laneHeader).ToHaveAttributeAsync("aria-expanded", "false");

        // The space's Summary tab, and "For you" from the sidebar (FR-021, FR-022).
        await TabToAsync(page, page.Locator("nav.space-tabs").GetByRole(AriaRole.Link, new() { Name = "Summary" }), backwards: true, maxPresses: 120);
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/spaces/KBL/summary");
        await TabToAsync(page, page.GetByTestId("summary-counts").GetByRole(AriaRole.Link).First);
        await TabToAsync(page, page.Locator("aside#sidebar").GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true }), backwards: true, maxPresses: 120);
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/for-you");
        await TabToAsync(page, page.Locator("nav[aria-label='For you']").GetByRole(AriaRole.Link, new() { Name = "Assigned to me" }));
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/for-you/assigned");
    }

    [Fact]
    public async Task JP_New_screens_work_at_360_px()
    {
        var password = await App.CreateUserAsync("pari.jp", "Pari Phone");
        var page = await SignInAsync("pari.jp", password);
        await CreateProjectAsync(page, "Phone Lanes", "PHL");
        await App.AddPlanAsync("PHL", [(Domain.Work.WorkItemType.Epic, "Card refunds", null, null, null), (Domain.Work.WorkItemType.Story, "Refund to card", "PHL-1", null, null)]);
        await page.SetViewportSizeAsync(360, 740);

        foreach (var path in new[] { "/for-you", "/for-you/starred", "/spaces/PHL/summary", "/spaces/PHL/board?group=epic", "/spaces" })
        {
            await GotoAsync(page, path);
            await AssertNoSidewaysScrollAsync(page);
        }

        // The sidebar opens as a panel from "Menu" and closes with Escape, giving focus back (FR-021).
        await GotoAsync(page, "/spaces/PHL/board");
        var menu = page.GetByTestId("menu-button");
        await menu.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("sidebar-panel").GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(page);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(menu).ToBeFocusedAsync();

        // The Create form and the template gallery fit the screen.
        await page.GetByTestId("create-button").ClickAsync();
        await Assertions.Expect(page.Locator("#create-summary")).ToBeFocusedAsync();
        await AssertNoSidewaysScrollAsync(page);
        await page.Keyboard.PressAsync("Escape");
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        await Assertions.Expect(page.GetByTestId("create-project").GetByRole(AriaRole.Radio)).ToHaveCountAsync(4);
        await AssertNoSidewaysScrollAsync(page);
    }

    [Fact]
    public async Task US1_Main_path_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("wasim", "Wasim Akram");
        var page = await SignInWithKeyboardAsync("wasim", password);
        await GotoAsync(page, "/spaces");

        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First);
        await page.Keyboard.PressAsync("Enter");
        var dialog = page.GetByTestId("create-project");
        await TabToAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(dialog.GetByLabel("Name")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Keyboard Board");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(dialog.GetByLabel("Key")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("KBD");
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/spaces/KBD/board");
        await WaitForInteractivityAsync(page);

        await TabToAsync(page, Column(page, 0).GetByPlaceholder("What needs to be done?"));
        foreach (var title in new[] { "Plan the sprint", "Write the brief" })
        {
            await page.Keyboard.TypeAsync(title);
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(Column(page, 0).Locator(".card-title", new() { HasText = title })).ToBeVisibleAsync();
        }

        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Actions for KBD-1" }), backwards: true);
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
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync("Keyboard Drawer");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("KDR");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, "/spaces/KDR/board");
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

        // Since Phase 4 a comment is formatted text: "Add a comment…" opens the editor, and Escape then Tab leaves it.
        await TabToAsync(page, drawer.GetByTestId("start-comment"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.Locator("#new-comment .rte-editable")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Ready for review.");
        await page.Keyboard.PressAsync("Escape");
        await TabToAsync(page, drawer.GetByTestId("new-comment-save"));
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
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync("Keyboard Columns");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("KCL");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, "/spaces/KCL/board");
        await GotoAsync(page, "/spaces/KCL/settings");

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

    [Fact]
    public async Task P2_The_members_list_timeline_and_My_tasks_screens_work_at_360_px()
    {
        var password = await App.CreateUserAsync("nasir", "Nasir Jamshed");
        var desk = await SignInAsync("nasir", password);
        await CreateProjectAsync(desk, "Phone Check", "PHN");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddScheduledTasksAsync("PHN",
        [
            ("Measure the room for the new layout", today.AddDays(1), today.AddDays(4)),
            ("Order the chairs", null, today.AddDays(-2)),
            ("Someday, maybe", null, null),
        ]);
        await App.AddTasksAsync("PHN", [("Mine to do, with a title long enough to wrap on a phone", today.AddDays(3), "nasir")]);

        var phone = await SignInAsync("nasir", password);
        await phone.SetViewportSizeAsync(360, 740);
        foreach (var (url, shown) in new[]
        {
            ("/spaces/PHN/members", phone.GetByTestId("member-row")),
            ("/spaces/PHN/list", phone.GetByTestId("list-row").First),
            ("/spaces/PHN/timeline", phone.Locator("button.tl-bar").First),
            ("/my-tasks", phone.GetByTestId("my-task").First),
        })
        {
            await GotoAsync(phone, url);
            await Assertions.Expect(shown).ToBeVisibleAsync();
            await AssertNoSidewaysScrollAsync(phone); // the list table and the timeline track scroll within their own areas
            await phone.AssertNoAccessibilityViolationsAsync();
        }
    }

    [Fact]
    public async Task P2_Main_paths_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("osman", "Osman Khawaja");
        await App.CreateUserAsync("pervez", "Pervez Rasool");
        var page = await SignInWithKeyboardAsync("osman", password);
        await CreateProjectAsync(page, "Keys Only", "KYB");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddScheduledTasksAsync("KYB", [("Measure the room", today.AddDays(1), today.AddDays(4)), ("Someday", null, null)]);
        var drawer = page.GetByTestId("drawer");

        // Story 1: add a Viewer, then make him a Member.
        await GotoAsync(page, "/spaces/KYB/members");
        await TabToAsync(page, page.Locator("#member-search"));
        await page.Keyboard.TypeAsync("perv");
        var person = page.GetByRole(AriaRole.Radio, new() { Name = "Pervez Rasool" });
        await TabToAsync(page, person);
        await page.Keyboard.PressAsync("Space");
        await TabToAsync(page, page.Locator("#member-role"));
        await page.Keyboard.PressAsync("ArrowDown"); // Member → Viewer
        await TabToAsync(page, page.Locator("#add-member"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("members-saved")).ToContainTextAsync("Pervez Rasool added as Viewer.");
        await Assertions.Expect(page.Locator("#member-search")).ToBeFocusedAsync();
        var role = page.GetByLabel("Role of Pervez Rasool");
        await TabToAsync(page, role);
        await page.Keyboard.PressAsync("ArrowUp"); // Viewer → Member
        await Assertions.Expect(page.GetByTestId("members-saved")).ToContainTextAsync("Pervez Rasool is now a Member.");
        await Assertions.Expect(role).ToBeFocusedAsync();

        // Story 2: open a task from the board and assign it to myself; find it on "My tasks".
        await GotoAsync(page, "/spaces/KYB/board");
        await TabToAsync(page, page.Locator("#card-KYB-1-title"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#drawer-heading")).ToBeFocusedAsync();
        await TabToAsync(page, drawer.GetByTestId("assign-to-me"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("drawer-saved")).ToContainTextAsync("Assigned to you");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await TabToAsync(page, page.GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true }), backwards: true);
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/for-you");
        await WaitForInteractivityAsync(page);
        await TabToAsync(page, page.GetByRole(AriaRole.Link, new() { Name = "Assigned to me" }));
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/for-you/assigned");
        await WaitForInteractivityAsync(page);
        var myTask = page.Locator("[data-key='KYB-1'] a.task-link");
        await TabToAsync(page, myTask);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("drawer-key")).ToHaveTextAsync("KYB-1");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(myTask).ToBeFocusedAsync();

        // Story 3: filter the list to my tasks and open one.
        await GotoAsync(page, "/spaces/KYB/list");
        await TabToAsync(page, page.Locator("#filter-assignee"));
        await page.Keyboard.PressAsync("ArrowDown"); // Anyone → Me
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("assignee=me"));
        await Assertions.Expect(page.GetByTestId("list-row")).ToHaveCountAsync(1);
        var row = page.Locator("[data-key='KYB-1'] a.task-link");
        await TabToAsync(page, row);
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(drawer.GetByTestId("drawer-key")).ToHaveTextAsync("KYB-1");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(row).ToBeFocusedAsync();

        // Story 4: move a bar one day later and schedule a task.
        await GotoAsync(page, "/spaces/KYB/timeline");
        var bar = page.Locator("button.tl-bar[data-key='KYB-1']");
        await TabToAsync(page, bar, maxPresses: 120);
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(bar).ToHaveAttributeAsync("aria-label", new Regex(Regex.Escape($"{Long(today.AddDays(2))} to {Long(today.AddDays(5))}")));
        await Assertions.Expect(bar).ToBeFocusedAsync();
        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Schedule KYB-2" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("button.tl-bar[data-key='KYB-2']")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task P3_The_create_project_backlog_report_stages_timeline_and_drawer_screens_work_at_360_px()
    {
        var password = await App.CreateUserAsync("rabia.p3", "Rabia Anwar");
        var desk = await SignInAsync("rabia.p3", password);
        await CreateProjectAsync(desk, "Phone Sprints", "PHS", "Scrum");
        await CreateProjectAsync(desk, "Phone Stages", "PHG", "Stage-gate");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddPlanAsync("PHS", [(Domain.Work.WorkItemType.Epic, "Card refunds with a title long enough to wrap", null, null, null)]);
        await App.AddStoriesAsync("PHS", [("Refund a card payment from the phone", 3), ("Show the refund in the history", 5)]);
        await App.AddSprintsAsync("PHS");
        await App.AddPlanAsync("PHG",
            [(Domain.Work.WorkItemType.Milestone, "Planning sign-off", "PHG-2", null, today.AddDays(20))],
            [("PHG-1", today, today.AddDays(13)), ("PHG-2", today.AddDays(14), today.AddDays(27))]);

        var phone = await SignInAsync("rabia.p3", password);
        await phone.SetViewportSizeAsync(360, 740);
        await GotoAsync(phone, "/spaces");
        await phone.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        await Assertions.Expect(phone.GetByTestId("create-project").GetByLabel("Stage-gate", new() { Exact = true })).ToBeVisibleAsync();
        await AssertNoSidewaysScrollAsync(phone);
        await phone.AssertNoAccessibilityViolationsAsync();

        foreach (var (url, shown) in new[]
        {
            ("/spaces/PHS/backlog", phone.GetByTestId("backlog-row").First),
            ("/spaces/PHS/sprints/1", phone.GetByTestId("report-title")),
            ("/spaces/PHS/board?task=PHS-1", phone.GetByTestId("container-items")),
            ("/spaces/PHS/timeline", phone.GetByTestId("sprint-lane")),
            ("/spaces/PHG/stages", phone.GetByTestId("stage").First),
            ("/spaces/PHG/timeline", phone.Locator("button.tl-milestone").First),
        })
        {
            await GotoAsync(phone, url);
            await Assertions.Expect(shown).ToBeVisibleAsync();
            await AssertNoSidewaysScrollAsync(phone); // the backlog rows wrap; the timeline track scrolls within its own area
            await phone.AssertNoAccessibilityViolationsAsync();
        }
    }

    [Fact]
    public async Task P3_Main_paths_with_the_keyboard_only()
    {
        var password = await App.CreateUserAsync("tariq.p3", "Tariq Jameel");
        var uzmaPassword = await App.CreateUserAsync("uzma.p3", "Uzma Gilani");
        var page = await SignInWithKeyboardAsync("tariq.p3", password);

        // Story 1: create a Scrum project; the template is a group of radio buttons chosen with the arrow keys.
        await GotoAsync(page, "/spaces");
        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First);
        await page.Keyboard.PressAsync("Enter");
        var create = page.GetByTestId("create-project");
        await TabToAsync(page, create.GetByLabel("Kanban", new() { Exact = true }));
        await page.Keyboard.PressAsync("ArrowDown"); // Kanban → Scrum
        await Assertions.Expect(create.GetByLabel("Scrum", new() { Exact = true })).ToBeCheckedAsync();
        await TabToAsync(page, create.GetByRole(AriaRole.Button, new() { Name = "Use template" }));
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, create.GetByLabel("Name"));
        await page.Keyboard.TypeAsync("Keys Sprints");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await TabToAsync(page, create.GetByLabel("Key"));
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("KBS");
        await TabToAsync(page, create.GetByRole(AriaRole.Button, new() { Name = "Create space" }));
        await page.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(page, "/spaces/KBS/board");
        await App.AddStoriesAsync("KBS", [("Refund a card payment", 3), ("Show the refund", 5)]);

        // Story 2: create a sprint, move a story into it with its Move group, and start it.
        await GotoAsync(page, "/spaces/KBS/backlog");
        await TabToAsync(page, page.GetByTestId("create-sprint"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("sprint-section")).ToHaveCountAsync(1);
        var move = page.Locator("#move-KBS-1");
        await TabToAsync(page, move);
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Move to KBS Sprint 1" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("sprint-section").GetByTestId("backlog-row")).ToHaveCountAsync(1);
        await Assertions.Expect(move).ToBeFocusedAsync();
        await TabToAsync(page, page.GetByTestId("start-sprint"), backwards: true);
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.GetByTestId("start-sprint-dialog").GetByRole(AriaRole.Button, new() { Name = "Start sprint" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("sprint-section").Locator(".sprint-state")).ToHaveTextAsync("Active");

        // Story 3: name uzma as the Initiation gate's approver and request approval; uzma approves it.
        await CreateProjectAsync(page, "Keys Stages", "KBG", "Stage-gate");
        await GotoAsync(page, "/spaces/KBG/stages");
        await TabToAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Set up the Initiation gate" }));
        await page.Keyboard.PressAsync("Enter");
        var setup = page.GetByTestId("gate-setup-dialog");
        await TabToAsync(page, setup.Locator("#approver-search"));
        await page.Keyboard.TypeAsync("Uzma");
        await TabToAsync(page, setup.GetByTestId("person-result").Filter(new() { HasText = "Uzma Gilani" }));
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, setup.GetByRole(AriaRole.Button, new() { Name = "Save gate" }));
        await page.Keyboard.PressAsync("Enter");
        var initiation = page.Locator("[data-testid=stage][data-key='KBG-1']");
        await Assertions.Expect(initiation.GetByTestId("gate-approver")).ToHaveCountAsync(1);
        await TabToAsync(page, initiation.GetByTestId("request-approval"));
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.GetByTestId("request-dialog").GetByRole(AriaRole.Button, new() { Name = "Request approval" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(initiation.GetByTestId("gate-state")).ToHaveTextAsync("Awaiting decision");

        var uzma = await SignInWithKeyboardAsync("uzma.p3", uzmaPassword);
        await GotoAsync(uzma, "/my-tasks");
        await TabToAsync(uzma, uzma.GetByTestId("gates-waiting").GetByRole(AriaRole.Link, new() { Name = "Initiation gate" }));
        await uzma.Keyboard.PressAsync("Enter");
        await WaitForPathAsync(uzma, "/spaces/KBG/stages?stage=KBG-1");
        await WaitForInteractivityAsync(uzma);
        await TabToAsync(uzma, uzma.GetByRole(AriaRole.Button, new() { Name = "Approve the Initiation gate" }));
        await uzma.Keyboard.PressAsync("Enter");
        await TabToAsync(uzma, uzma.GetByTestId("decision-dialog").GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }));
        await uzma.Keyboard.PressAsync("Enter");
        await Assertions.Expect(uzma.Locator("[data-testid=stage][data-key='KBG-1']").GetByTestId("gate-state")).ToHaveTextAsync("Approved");

        // Story 4: move a milestone a day later, and add a dependency from a row's Actions.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddPlanAsync("KBG",
        [
            (Domain.Work.WorkItemType.Milestone, "Planning sign-off", "KBG-2", null, today.AddDays(10)),
            (Domain.Work.WorkItemType.Task, "Write the charter", "KBG-1", today, today.AddDays(4)),
            (Domain.Work.WorkItemType.Task, "Agree the budget", "KBG-1", today.AddDays(5), today.AddDays(9)),
        ]);
        await GotoAsync(page, "/spaces/KBG/timeline");
        var milestone = page.Locator("button.tl-milestone[data-key='KBG-5']");
        await TabToAsync(page, milestone, maxPresses: 120);
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(milestone).ToHaveAttributeAsync("aria-label", new Regex(Regex.Escape($"milestone on {Long(today.AddDays(11))}")));
        await Assertions.Expect(milestone).ToBeFocusedAsync();
        await TabToAsync(page, page.Locator("#tl-actions-KBG-7"), maxPresses: 120);
        await page.Keyboard.PressAsync("Enter");
        await TabToAsync(page, page.Locator("#tl-actions-KBG-7-menu").GetByRole(AriaRole.Button, new() { Name = "Add dependency" }));
        await page.Keyboard.PressAsync("Enter");
        var dialog = page.GetByTestId("dependency-dialog");
        await TabToAsync(page, dialog.Locator("#dependency-search"));
        await page.Keyboard.TypeAsync("KBG-6");
        await TabToAsync(page, dialog.GetByTestId("dependency-result").Filter(new() { HasText = "KBG-6" }));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("button.tl-bar[data-key='KBG-7']")).ToHaveAttributeAsync("aria-label", new Regex("waits for KBG-6$"));
    }
}
