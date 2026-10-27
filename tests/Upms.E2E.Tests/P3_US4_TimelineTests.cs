using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 3 User Story 4 end to end: the timeline as a plan, with stages and their gates, a milestone, a
/// dependency drawn as an arrow and flagged late, and a Scrum project's sprint lane.</summary>
public sealed class P3_US4_TimelineTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Bar(IPage page, string key) => page.Locator($"button.tl-bar[data-key='{key}']");

    private static ILocator Group(IPage page, string key) => page.Locator($"[data-testid=tl-group][data-key='{key}']");

    private static string Long(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static async Task ActAsync(IPage page, string key, string action)
    {
        await page.Locator($"#tl-actions-{key}").ClickAsync();
        await page.Locator($"#tl-actions-{key}-menu").GetByRole(AriaRole.Button, new() { Name = action, Exact = true }).ClickAsync();
    }

    /// <summary>Drags a bar's right end by a number of pixels, as a person would with a mouse.</summary>
    private static async Task DragEndAsync(IPage page, string key, double pixels)
    {
        await Bar(page, key).ScrollIntoViewIfNeededAsync();
        var box = (await Bar(page, key).BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width - 4, box.Y + (box.Height / 2));
        await page.Mouse.MoveAsync((float)x, (float)y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(x + pixels), (float)y, new() { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task P3_US4_Independent_test_plan_a_stage_gate_project_on_the_timeline()
    {
        var hinaPassword = await App.CreateUserAsync("hina.p4", "Hina Pervez");
        var zaidPassword = await App.CreateUserAsync("zaid.p4", "Zaid Viewer");
        var hina = await SignInAsync("hina.p4", hinaPassword);
        await GotoAsync(hina, "/spaces");
        await hina.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = hina.GetByTestId("create-project");
        await create.GetByLabel("Stage-gate", new() { Exact = true }).CheckAsync();
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Core Banking Plan");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("CBP");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(hina, "/spaces/CBP/board");
        await App.AddMemberAsync("CBP", "zaid.p4", ProjectRole.Viewer);

        // The four stages (CBP-1 to CBP-4) get dates; a milestone (CBP-5) and six tasks (CBP-6 to CBP-11) go in them.
        var today = DateOnly.FromDateTime(DateTime.UtcNow); // the organization's time zone is UTC
        await App.AddPlanAsync("CBP",
        [
            (WorkItemType.Milestone, "Planning sign-off", "CBP-2", null, today.AddDays(27)),
            (WorkItemType.Task, "Write the charter", "CBP-1", today, today.AddDays(4)),
            (WorkItemType.Task, "Agree the budget", "CBP-1", today.AddDays(5), today.AddDays(10)),
            (WorkItemType.Task, "Plan the migration", "CBP-2", today.AddDays(14), today.AddDays(20)),
            (WorkItemType.Task, "Choose a vendor", "CBP-2", today.AddDays(21), today.AddDays(26)),
            (WorkItemType.Task, "Build the interfaces", "CBP-3", today.AddDays(28), today.AddDays(40)),
            (WorkItemType.Task, "Test the migration", "CBP-3", today.AddDays(41), today.AddDays(50)),
        ],
        [
            ("CBP-1", today, today.AddDays(13)),
            ("CBP-2", today.AddDays(14), today.AddDays(27)),
            ("CBP-3", today.AddDays(28), today.AddDays(55)),
            ("CBP-4", today.AddDays(56), today.AddDays(69)),
        ]);

        // P3_US4_AS1 and AS3: the stages in stage order, with their progress and a gate marker on each end date.
        await GotoAsync(hina, "/spaces/CBP/timeline");
        await WaitForInteractivityAsync(hina);
        await Assertions.Expect(hina.GetByTestId("tl-group")).ToHaveCountAsync(4);
        Assert.Equal(["CBP-1", "CBP-2", "CBP-3", "CBP-4"],
            await hina.GetByTestId("tl-group").EvaluateAllAsync<string[]>("groups => groups.map(g => g.dataset.key)"));
        await Assertions.Expect(Group(hina, "CBP-1").GetByTestId("tl-progress")).ToHaveTextAsync("0% done");
        await Assertions.Expect(Group(hina, "CBP-1").GetByTestId("gate-marker")).ToContainTextAsync("Gate not requested");
        await Assertions.Expect(hina.GetByTestId("gate-marker")).ToHaveCountAsync(4);

        // P3_US4_AS2: the milestone is a marker on its date, with no handles.
        var milestone = hina.Locator("button.tl-milestone[data-key='CBP-5']");
        await Assertions.Expect(milestone).ToHaveAttributeAsync("aria-label", new Regex($"milestone on {Regex.Escape(Long(today.AddDays(27)))}"));
        await Assertions.Expect(milestone.Locator(".tl-handle")).ToHaveCountAsync(0);

        // P3_US4_AS5: CBP-11 waits for CBP-10, added from its row's Actions menu; an arrow joins the two bars.
        await ActAsync(hina, "CBP-11", "Add dependency");
        var dialog = hina.GetByTestId("dependency-dialog");
        await dialog.Locator("#dependency-search").FillAsync("CBP-10");
        await hina.AssertNoAccessibilityViolationsAsync();
        await dialog.GetByTestId("dependency-result").Filter(new() { HasText = "CBP-10" }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(Bar(hina, "CBP-11")).ToHaveAttributeAsync("aria-label", new Regex("waits for CBP-10$"));
        await Assertions.Expect(hina.Locator("svg.tl-arrows path.tl-arrow")).ToHaveCountAsync(1);
        await Assertions.Expect(hina.Locator("svg.tl-arrows")).ToHaveAttributeAsync("aria-hidden", "true");

        // P3_US4_AS6: dragging CBP-10's end three days past CBP-11's start flags the link late; no other date moves.
        await DragEndAsync(hina, "CBP-10", 3 * 8); // months: 8 px a day
        await Assertions.Expect(Bar(hina, "CBP-10")).ToHaveAttributeAsync("aria-label",
            new Regex(Regex.Escape($"{Long(today.AddDays(28))} to {Long(today.AddDays(43))}")));
        await Assertions.Expect(Bar(hina, "CBP-11")).ToHaveAttributeAsync("aria-label",
            new Regex($"{Regex.Escape($"{Long(today.AddDays(41))} to {Long(today.AddDays(50))}")}.*waits for CBP-10 \\(late\\)$"));
        await Assertions.Expect(hina.Locator("svg.tl-arrows path.tl-arrow.tl-arrow-late")).ToHaveCountAsync(1);
        await hina.AssertNoAccessibilityViolationsAsync();

        // Both drawers say so, in words.
        foreach (var key in new[] { "CBP-10", "CBP-11" })
        {
            await GotoAsync(hina, $"/spaces/CBP/timeline?task={key}");
            var dependencies = hina.GetByTestId("drawer").GetByTestId("dependencies");
            await Assertions.Expect(dependencies.GetByTestId("late")).ToHaveTextAsync("Late");
        }

        // P3_US4_AS2: the milestone moves two days later with the keyboard; Shift moves it the same way.
        await GotoAsync(hina, "/spaces/CBP/timeline");
        await WaitForInteractivityAsync(hina);
        await milestone.FocusAsync();
        await hina.Keyboard.PressAsync("ArrowRight");
        await hina.Keyboard.PressAsync("Shift+ArrowRight");
        await hina.Keyboard.PressAsync("Enter");
        await Assertions.Expect(milestone).ToHaveAttributeAsync("aria-label", new Regex($"milestone on {Regex.Escape(Long(today.AddDays(29)))}"));
        await Assertions.Expect(milestone).ToBeFocusedAsync();

        // P3_US4_AS9: a Viewer sees the plan but moves nothing and adds no dependency.
        var zaid = await SignInAsync("zaid.p4", zaidPassword);
        await GotoAsync(zaid, "/spaces/CBP/timeline");
        await WaitForInteractivityAsync(zaid);
        await Assertions.Expect(zaid.GetByTestId("tl-group")).ToHaveCountAsync(4);
        await Assertions.Expect(zaid.Locator(".tl-handle")).ToHaveCountAsync(0);
        await Assertions.Expect(zaid.Locator("svg.tl-arrows path.tl-arrow")).ToHaveCountAsync(1);
        await zaid.Locator("#tl-actions-CBP-11").ClickAsync();
        await Assertions.Expect(zaid.Locator("#tl-actions-CBP-11-menu").GetByRole(AriaRole.Button)).ToHaveTextAsync(["Open"]);
        await zaid.AssertNoAccessibilityViolationsAsync();

        // Narrow screens: the track scrolls sideways within its own area.
        await hina.SetViewportSizeAsync(360, 740);
        await hina.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task P3_US4_A_Scrum_timeline_shows_its_sprints_and_an_epic_is_scheduled_from_its_menu()
    {
        var noorPassword = await App.CreateUserAsync("noor.p4", "Noor Fatima");
        var noor = await SignInAsync("noor.p4", noorPassword);
        await GotoAsync(noor, "/spaces");
        await noor.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = noor.GetByTestId("create-project");
        await create.GetByLabel("Scrum", new() { Exact = true }).CheckAsync();
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Mobile Plan");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("MPL");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(noor, "/spaces/MPL/board");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddPlanAsync("MPL",
        [
            (WorkItemType.Epic, "Card refunds", null, null, null),
            (WorkItemType.Story, "Refund a card payment", "MPL-1", today, today.AddDays(5)),
        ]);
        await App.AddSprintsAsync("MPL");

        // P3_US4_AS8: the sprint lane shows the completed and the active sprint.
        await GotoAsync(noor, "/spaces/MPL/timeline");
        await WaitForInteractivityAsync(noor);
        var sprints = noor.GetByTestId("sprint-lane").GetByTestId("sprint-bar");
        await Assertions.Expect(sprints).ToHaveCountAsync(2);
        await Assertions.Expect(sprints.Nth(0)).ToContainTextAsync("MPL Sprint 1 · completed");
        await Assertions.Expect(sprints.Nth(1)).ToContainTextAsync("MPL Sprint 2 · active");

        // P3_US4_AS4: the undated epic gets four weeks from today.
        await Assertions.Expect(Group(noor, "MPL-1")).ToContainTextAsync("No dates");
        await ActAsync(noor, "MPL-1", "Schedule");
        await Assertions.Expect(Bar(noor, "MPL-1")).ToHaveAttributeAsync("aria-label",
            new Regex(Regex.Escape($"MPL-1 Card refunds, {Long(today)} to {Long(today.AddDays(27))}")));
        await noor.AssertNoAccessibilityViolationsAsync();
        await noor.SetViewportSizeAsync(360, 740);
        await noor.AssertNoAccessibilityViolationsAsync();
    }
}
