using System.Globalization;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 2 User Story 2 end to end: assignees and dates in the drawer, on cards and in the board filters, and
/// each person's "My tasks".</summary>
public sealed class P2_US2_AssignAndScheduleTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task CreateProjectAsync(IPage page, string name, string key)
    {
        await GotoAsync(page, "/projects");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync(name);
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await WaitForPathAsync(page, $"/projects/{key}/board");
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
        await Assertions.Expect(Drawer(page).Locator("#drawer-assignee")).ToBeEnabledAsync();
    }

    private static async Task CloseDrawerAsync(IPage page)
    {
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Drawer(page)).ToBeHiddenAsync();
    }

    private static async Task AssignAsync(IPage page, string displayName)
    {
        await Drawer(page).Locator("#drawer-assignee").SelectOptionAsync(new SelectOptionValue { Label = displayName });
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-saved")).ToContainTextAsync($"Assigned to {displayName}");
    }

    private static async Task SetDueDateAsync(IPage page, DateTime due)
    {
        await Drawer(page).Locator("#drawer-due").FillAsync(Iso(due));
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-saved")).ToContainTextAsync("Due date saved");
    }

    [Fact]
    public async Task P2_US2_Independent_test_assign_and_schedule_tasks_and_find_them_on_My_tasks()
    {
        var rashidPassword = await App.CreateUserAsync("rashid", "Rashid Latif");
        var sadiaPassword = await App.CreateUserAsync("sadia", "Sadia Karim");
        await App.CreateUserAsync("noman", "Noman Iqbal");
        // Two days back, so the task is overdue in every viewer's time zone (FR-042).
        var overdue = DateTime.UtcNow.Date.AddDays(-2);
        var nextWeek = DateTime.UtcNow.Date.AddDays(7);

        var rashid = await SignInAsync("rashid", rashidPassword);
        await CreateProjectAsync(rashid, "Support Desk", "SUP");
        await AddTaskAsync(rashid, "Answer the backlog");
        await App.AddMemberAsync("SUP", "sadia");
        await CreateProjectAsync(rashid, "Launch Plan", "LCH");
        foreach (var title in new[] { "Book the venue", "Print the flyers", "Invite the press" })
        {
            await AddTaskAsync(rashid, title);
        }

        await App.AddMemberAsync("LCH", "sadia");
        await App.AddMemberAsync("LCH", "noman");
        await rashid.ReloadAsync();
        await WaitForInteractivityAsync(rashid);

        // P2_US2_AS1 and AS4: the Project Admin assigns LCH-1 to Sadia, due two days ago, and LCH-2 to Noman, due next week.
        await OpenCardAsync(rashid, "LCH-1");
        await Assertions.Expect(Drawer(rashid).Locator("#drawer-assignee option")).ToHaveTextAsync(
            ["Unassigned", "Noman Iqbal", "Rashid Latif (me)", "Sadia Karim"]);
        await AssignAsync(rashid, "Sadia Karim");
        await SetDueDateAsync(rashid, overdue);
        await Assertions.Expect(Drawer(rashid).GetByTestId("drawer-dates").Locator(".overdue-label")).ToHaveTextAsync("Overdue");
        await rashid.AssertNoAccessibilityViolationsAsync();
        await CloseDrawerAsync(rashid);
        await OpenCardAsync(rashid, "LCH-2");
        await AssignAsync(rashid, "Noman Iqbal");
        await SetDueDateAsync(rashid, nextWeek);
        await CloseDrawerAsync(rashid);

        // P2_US2_AS4: a due date before the start date is refused, and what was typed stays.
        await OpenCardAsync(rashid, "LCH-2");
        await Drawer(rashid).Locator("#drawer-start").FillAsync(Iso(nextWeek.AddDays(3)));
        await Assertions.Expect(Drawer(rashid).Locator("#drawer-dates-error")).ToHaveTextAsync("The due date cannot be before the start date.");
        await Assertions.Expect(Drawer(rashid).Locator("#drawer-start")).ToHaveValueAsync(Iso(nextWeek.AddDays(3)));
        await CloseDrawerAsync(rashid);

        // Support Desk: a task for Sadia in another project.
        await GotoAsync(rashid, "/projects/SUP/board");
        await OpenCardAsync(rashid, "SUP-1");
        await AssignAsync(rashid, "Sadia Karim");
        await CloseDrawerAsync(rashid);

        // P2_US2_AS2: Sadia assigns LCH-3 to herself in one step.
        var sadia = await SignInAsync("sadia", sadiaPassword);
        await GotoAsync(sadia, "/projects/LCH/board");
        await OpenCardAsync(sadia, "LCH-3");
        await Drawer(sadia).GetByTestId("assign-to-me").ClickAsync();
        await Assertions.Expect(Drawer(sadia).GetByTestId("drawer-saved")).ToContainTextAsync("Assigned to you");
        await CloseDrawerAsync(sadia);

        // The cards show assignees and due dates, LCH-1 marked overdue in words (P2_US2_AS5).
        var card1 = sadia.GetByTestId("card-LCH-1");
        await Assertions.Expect(card1.Locator(".avatar")).ToHaveTextAsync("SK");
        await Assertions.Expect(card1).ToContainTextAsync("Assigned to Sadia Karim");
        await Assertions.Expect(card1.Locator(".overdue-label")).ToHaveTextAsync("Overdue");
        await Assertions.Expect(sadia.GetByTestId("card-LCH-2")).ToContainTextAsync("Assigned to Noman Iqbal");
        await Assertions.Expect(sadia.GetByTestId("card-LCH-2").Locator(".overdue-label")).ToHaveCountAsync(0);

        // P2_US2_AS6: "Only my tasks" leaves LCH-1 and LCH-3, and the column says how many match.
        await sadia.GetByTestId("only-mine").ClickAsync();
        await Assertions.Expect(sadia.Locator("article.bcard")).ToHaveCountAsync(2);
        await Assertions.Expect(Column(sadia, 0).Locator(".count")).ToHaveTextAsync("2 of 3");
        await sadia.AssertNoAccessibilityViolationsAsync();
        await sadia.GetByTestId("only-mine").ClickAsync();
        await Assertions.Expect(sadia.Locator("article.bcard")).ToHaveCountAsync(3);

        // P2_US2_AS7: "My tasks" lists her open tasks by project, soonest due first.
        await sadia.GetByRole(AriaRole.Link, new() { Name = "My tasks" }).ClickAsync();
        await WaitForPathAsync(sadia, "/my-tasks");
        await WaitForInteractivityAsync(sadia);
        var rows = sadia.GetByTestId("my-task");
        await Assertions.Expect(rows).ToHaveCountAsync(3);
        Assert.Equal(["LCH-1", "LCH-3", "SUP-1"], await rows.EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.key)"));
        await Assertions.Expect(sadia.Locator("[data-key='LCH-1']")).ToContainTextAsync("Overdue");
        await Assertions.Expect(sadia.GetByTestId("my-tasks-count")).ToHaveTextAsync("3 open tasks are assigned to you.");
        await sadia.AssertNoAccessibilityViolationsAsync();

        // FR-026: completing a task in the drawer takes it off the list.
        await sadia.Locator("[data-key='LCH-1'] a.task-link").ClickAsync();
        await Assertions.Expect(Drawer(sadia).GetByTestId("drawer-key")).ToHaveTextAsync("LCH-1");
        await Drawer(sadia).Locator("#drawer-status").SelectOptionAsync(new SelectOptionValue { Label = "Done" });
        await Assertions.Expect(Drawer(sadia).GetByTestId("drawer-saved")).ToContainTextAsync("Status changed to Done");
        await CloseDrawerAsync(sadia);
        await Assertions.Expect(rows).ToHaveCountAsync(2);
        await Assertions.Expect(sadia.Locator("[data-key='LCH-1']")).ToHaveCountAsync(0);

        // Narrow screens (FR-041).
        await sadia.SetViewportSizeAsync(360, 740);
        await sadia.AssertNoAccessibilityViolationsAsync();
        await GotoAsync(sadia, "/projects/LCH/board");
        await sadia.AssertNoAccessibilityViolationsAsync();
        await OpenCardAsync(sadia, "LCH-2");
        await sadia.AssertNoAccessibilityViolationsAsync();
    }
}
