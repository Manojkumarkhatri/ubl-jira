using System.Globalization;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 2 User Story 4 end to end: plan on a timeline with the mouse and with the keyboard.</summary>
public sealed class P2_US4_TimelineTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Bar(IPage page, string key) => page.Locator($"button.tl-bar[data-key='{key}']");

    private static string Long(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Drags from a point on the bar by a number of pixels, as a person would with a mouse.</summary>
    private static async Task DragAsync(IPage page, string key, double fromRight, double pixels)
    {
        var box = (await Bar(page, key).BoundingBoxAsync())!;
        var x = fromRight > 0 ? box.X + box.Width - fromRight : box.X + (box.Width / 2);
        var y = box.Y + (box.Height / 2);
        await page.Mouse.MoveAsync((float)x, (float)y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(x + pixels), (float)y, new() { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task P2_US4_Independent_test_move_resize_and_schedule_tasks_on_the_timeline()
    {
        var kamranPassword = await App.CreateUserAsync("kamran", "Kamran Akmal");
        var lubnaPassword = await App.CreateUserAsync("lubna", "Lubna Saeed");
        var kamran = await SignInAsync("kamran", kamranPassword);
        await GotoAsync(kamran, "/projects");
        await kamran.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = kamran.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Roadmap");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("RMP");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await WaitForPathAsync(kamran, "/projects/RMP/board");
        await App.AddMemberAsync("RMP", "lubna", Upms.Domain.Projects.ProjectRole.Viewer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow); // the organization's time zone is UTC
        await App.AddScheduledTasksAsync("RMP",
        [
            ("Kick-off", today.AddDays(2), today.AddDays(8)),
            ("Research", today.AddDays(5), today.AddDays(12)),
            ("Design", today.AddDays(10), today.AddDays(20)),
            ("Review", null, today.AddDays(25)),
            ("Launch party", null, null),
            ("Retrospective", null, null),
        ]);

        // P2_US4_AS1 and AS2: four bars (one of them a one-day bar), a today line, and two unscheduled tasks.
        await GotoAsync(kamran, "/projects/RMP/board");
        await kamran.Locator("nav.project-nav").GetByRole(AriaRole.Link, new() { Name = "Timeline" }).ClickAsync();
        await WaitForPathAsync(kamran, "/projects/RMP/timeline");
        await WaitForInteractivityAsync(kamran);
        await Assertions.Expect(kamran.Locator("button.tl-bar")).ToHaveCountAsync(4);
        await Assertions.Expect(kamran.Locator(".tl-today")).ToHaveCountAsync(1);
        await Assertions.Expect(kamran.GetByTestId("unscheduled-item")).ToHaveCountAsync(2);
        await Assertions.Expect(Bar(kamran, "RMP-4")).ToHaveAttributeAsync("aria-label", $"RMP-4 Review, due {Long(today.AddDays(25))}, To do, unassigned");
        await kamran.AssertNoAccessibilityViolationsAsync();

        // P2_US4_AS3: dragging a bar two weeks later moves both dates (months: 8 px a day).
        await DragAsync(kamran, "RMP-1", fromRight: 0, pixels: 14 * 8);
        await Assertions.Expect(Bar(kamran, "RMP-1")).ToHaveAttributeAsync("aria-label",
            $"RMP-1 Kick-off, {Long(today.AddDays(16))} to {Long(today.AddDays(22))}, To do, unassigned");

        // P2_US4_AS4: dragging a bar's right end three days later changes only the due date.
        await DragAsync(kamran, "RMP-2", fromRight: 4, pixels: 3 * 8);
        await Assertions.Expect(Bar(kamran, "RMP-2")).ToHaveAttributeAsync("aria-label",
            $"RMP-2 Research, {Long(today.AddDays(5))} to {Long(today.AddDays(15))}, To do, unassigned");

        // P2_US4_AS5: the keyboard moves a bar one day earlier; Enter saves.
        await Bar(kamran, "RMP-3").FocusAsync();
        await kamran.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(Bar(kamran, "RMP-3")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-pending"));
        await kamran.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Bar(kamran, "RMP-3")).ToHaveAttributeAsync("aria-label",
            $"RMP-3 Design, {Long(today.AddDays(9))} to {Long(today.AddDays(19))}, To do, unassigned");
        await Assertions.Expect(Bar(kamran, "RMP-3")).ToBeFocusedAsync();

        // P2_US4_AS6: "Schedule" gives an unscheduled task today to six days later.
        await kamran.GetByRole(AriaRole.Button, new() { Name = "Schedule RMP-5" }).ClickAsync();
        await Assertions.Expect(Bar(kamran, "RMP-5")).ToHaveAttributeAsync("aria-label",
            $"RMP-5 Launch party, {Long(today)} to {Long(today.AddDays(6))}, To do, unassigned");
        await Assertions.Expect(kamran.GetByTestId("unscheduled-item")).ToHaveCountAsync(1);

        // The history of each task records exactly these changes.
        await Bar(kamran, "RMP-1").FocusAsync();
        await kamran.Keyboard.PressAsync("Enter"); // nothing pending: opens the drawer
        var drawer = kamran.GetByTestId("drawer");
        await Assertions.Expect(drawer.GetByTestId("drawer-key")).ToHaveTextAsync("RMP-1");
        await Assertions.Expect(drawer.Locator("#drawer-start")).ToHaveValueAsync(today.AddDays(16).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await drawer.Locator("summary", new() { HasText = "History" }).ClickAsync();
        var history = drawer.GetByTestId("history-entry"); // created, the first dates, then the drag as one edit
        await Assertions.Expect(history.Nth(3)).ToContainTextAsync($"changed the start date from {Long(today.AddDays(2))} to {Long(today.AddDays(16))}");
        await Assertions.Expect(history.Nth(4)).ToContainTextAsync($"changed the due date from {Long(today.AddDays(8))} to {Long(today.AddDays(22))}");
        await kamran.Keyboard.PressAsync("Escape");
        await Assertions.Expect(drawer).ToBeHiddenAsync();

        // P2_US4_AS9: a Viewer reads the timeline but has nothing to drag or schedule.
        var lubna = await SignInAsync("lubna", lubnaPassword);
        await GotoAsync(lubna, "/projects/RMP/timeline?scale=weeks");
        await Assertions.Expect(lubna.Locator("button.tl-bar")).ToHaveCountAsync(5);
        await Assertions.Expect(lubna.Locator(".tl-handle")).ToHaveCountAsync(0);
        await Assertions.Expect(lubna.Locator("button.schedule-btn")).ToHaveCountAsync(0);
        await lubna.AssertNoAccessibilityViolationsAsync();

        // Narrow screens: the track scrolls sideways within its own area (FR-041).
        await kamran.SetViewportSizeAsync(360, 740);
        await kamran.AssertNoAccessibilityViolationsAsync();
    }
}
