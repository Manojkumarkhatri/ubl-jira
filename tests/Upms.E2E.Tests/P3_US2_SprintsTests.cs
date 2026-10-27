using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 3 User Story 2 end to end: an ordered backlog, a sprint planned by mouse and keyboard, the Scrum board
/// and the sprint report.</summary>
public sealed class P3_US2_SprintsTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Sprint(IPage page) => page.Locator("[data-testid=sprint-section]").First;

    private static ILocator BacklogRows(IPage page) => page.Locator("[data-testid=backlog-section] [data-testid=backlog-row]");

    private static ILocator Row(IPage page, string key) => page.Locator($"[data-testid=backlog-row][data-key='{key}']");

    [Fact]
    public async Task P3_US2_Independent_test_plan_run_and_report_a_sprint()
    {
        var samPassword = await App.CreateUserAsync("sam.p3", "Sam Iqbal");
        var sam = await SignInAsync("sam.p3", samPassword);
        await GotoAsync(sam, "/spaces");
        await sam.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = sam.GetByTestId("create-project");
        await dialog.GetByLabel("Scrum", new() { Exact = true }).CheckAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync("Sprint App");
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("SPM");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sam, "/spaces/SPM/board");
        await App.AddStoriesAsync("SPM", Enumerable.Range(1, 15).Select(n => ($"Refund story {n}", (decimal)(n % 5 + 1))));

        // P3_US2_AS9: with no active sprint the board links to the backlog.
        await GotoAsync(sam, "/spaces/SPM/board");
        await sam.GetByTestId("plan-sprint").ClickAsync();
        await WaitForPathAsync(sam, "/spaces/SPM/backlog");
        await WaitForInteractivityAsync(sam);
        await Assertions.Expect(BacklogRows(sam)).ToHaveCountAsync(15);

        // P3_US2_AS2: "Move to top", and focus stays on the moved row.
        await sam.Locator("#move-SPM-15").ClickAsync();
        await Row(sam, "SPM-15").GetByRole(AriaRole.Button, new() { Name = "Move to top" }).ClickAsync();
        await Assertions.Expect(BacklogRows(sam).First).ToHaveAttributeAsync("data-key", "SPM-15");
        await Assertions.Expect(sam.Locator("#move-SPM-15")).ToBeFocusedAsync();

        // P3_US2_AS3: a Project Admin creates "SPM Sprint 1"; five items go in by mouse and one by keyboard.
        await sam.GetByTestId("create-sprint").ClickAsync();
        await Assertions.Expect(Sprint(sam).Locator("h2")).ToHaveTextAsync("SPM Sprint 1");
        await sam.AssertNoAccessibilityViolationsAsync();
        foreach (var (key, count) in new[] { ("SPM-15", 1), ("SPM-1", 2), ("SPM-2", 3), ("SPM-3", 4), ("SPM-4", 5) })
        {
            await Row(sam, key).DragToAsync(Sprint(sam).GetByTestId("list-drop-end"));
            await Assertions.Expect(Sprint(sam).GetByTestId("backlog-row")).ToHaveCountAsync(count);
        }

        await sam.Locator("#move-SPM-5").FocusAsync();
        await sam.Keyboard.PressAsync("Enter");
        await Row(sam, "SPM-5").GetByRole(AriaRole.Button, new() { Name = "Move to SPM Sprint 1" }).FocusAsync();
        await sam.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Sprint(sam).GetByTestId("backlog-row")).ToHaveCountAsync(6);
        await Assertions.Expect(sam.Locator("#move-SPM-5")).ToBeFocusedAsync();
        await Assertions.Expect(BacklogRows(sam)).ToHaveCountAsync(9);

        // P3_US2_AS5: started for two weeks from today.
        await Sprint(sam).GetByTestId("start-sprint").ClickAsync();
        var start = sam.GetByTestId("start-sprint-dialog");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assertions.Expect(start.Locator("#sprint-end")).ToHaveValueAsync(today.AddDays(14).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        await start.Locator("#sprint-start-goal").FillAsync("Ship card refunds");
        await sam.AssertNoAccessibilityViolationsAsync();
        await start.GetByRole(AriaRole.Button, new() { Name = "Start sprint" }).ClickAsync();
        await Assertions.Expect(Sprint(sam).Locator(".sprint-state")).ToHaveTextAsync("Active");
        await sam.AssertNoAccessibilityViolationsAsync();
        await sam.SetViewportSizeAsync(360, 740);
        await sam.AssertNoAccessibilityViolationsAsync();
        await sam.SetViewportSizeAsync(1280, 900);

        // The board shows only the sprint's six items; four are completed and one is added during the sprint.
        await GotoAsync(sam, "/spaces/SPM/board");
        await Assertions.Expect(sam.GetByTestId("sprint-banner").Locator("h2")).ToHaveTextAsync("SPM Sprint 1");
        await Assertions.Expect(sam.Locator("article.bcard")).ToHaveCountAsync(6);
        foreach (var key in new[] { "SPM-15", "SPM-1", "SPM-2", "SPM-3" })
        {
            var card = sam.GetByTestId($"card-{key}");
            await card.Locator(".move-btn").ClickAsync();
            await card.GetByRole(AriaRole.Button, new() { Name = "Done: bottom" }).ClickAsync();
            await Assertions.Expect(sam.Locator("[data-testid=column]").Last.GetByTestId($"card-{key}")).ToBeVisibleAsync();
        }

        var box = sam.Locator("[data-testid=column]").First.GetByPlaceholder("What needs to be done?");
        await box.FillAsync("Refund hotfix");
        await box.PressAsync("Enter");
        await Assertions.Expect(sam.Locator("article.bcard")).ToHaveCountAsync(7);
        await sam.AssertNoAccessibilityViolationsAsync();

        // P3_US2_AS7: completed, with the three open items back in the backlog.
        await sam.GetByTestId("complete-sprint").ClickAsync();
        var complete = sam.GetByTestId("complete-sprint-dialog");
        await Assertions.Expect(complete).ToContainTextAsync("4 items are done and 3 are open.");
        await sam.AssertNoAccessibilityViolationsAsync();
        await complete.GetByRole(AriaRole.Button, new() { Name = "Complete sprint" }).ClickAsync();
        await Assertions.Expect(sam.GetByTestId("plan-sprint")).ToBeVisibleAsync();

        // P3_US2_AS8: the report.
        await GotoAsync(sam, "/spaces/SPM/backlog");
        await Assertions.Expect(BacklogRows(sam)).ToHaveCountAsync(12);
        await sam.GetByTestId("past-sprints").GetByRole(AriaRole.Link, new() { Name = "SPM Sprint 1" }).ClickAsync();
        await WaitForPathAsync(sam, "/spaces/SPM/sprints/1");
        var totals = sam.GetByTestId("group-total");
        await Assertions.Expect(totals.Nth(0)).ToContainTextAsync("6 items");
        await Assertions.Expect(totals.Nth(1)).ToContainTextAsync("1 item");
        await Assertions.Expect(totals.Nth(3)).ToContainTextAsync("4 items");
        await Assertions.Expect(totals.Nth(4)).ToContainTextAsync("3 items");
        await sam.AssertNoAccessibilityViolationsAsync();
        await sam.SetViewportSizeAsync(360, 740);
        await sam.AssertNoAccessibilityViolationsAsync();
    }
}
