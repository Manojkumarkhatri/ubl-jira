using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 3 User Story 3 end to end: a gate named by the Project Admin sara, with farah (from outside the
/// project) and omar as approvers, requested by amina, sent back, requested again and approved.</summary>
public sealed class P3_US3_StageGatesTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Stage(IPage page, string key) => page.Locator($"[data-testid=stage][data-key='{key}']");

    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static async Task AddToStageAsync(IPage page, string stageKey, string title)
    {
        await GotoAsync(page, $"/spaces/CBU/board?task={stageKey}");
        var items = Drawer(page).GetByTestId("container-items");
        await items.Locator("#container-add-input").FillAsync(title);
        await items.Locator("#container-add-input").PressAsync("Enter");
        await Assertions.Expect(items).ToContainTextAsync(title);
    }

    private static async Task DecideAsync(IPage page, string button, string? note = null)
    {
        await GotoAsync(page, "/my-tasks");
        await page.GetByTestId("gates-waiting").GetByRole(AriaRole.Link, new() { Name = "Initiation gate" }).ClickAsync();
        await WaitForPathAsync(page, "/spaces/CBU/stages?stage=CBU-1");
        await WaitForInteractivityAsync(page);
        await Stage(page, "CBU-1").GetByTestId(button == "Approve" ? "approve-gate" : "send-back-gate").ClickAsync();
        var dialog = page.GetByTestId("decision-dialog");
        if (note is not null)
        {
            await dialog.Locator("#gate-note").FillAsync(note);
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
    }

    private static async Task RequestAsync(IPage page)
    {
        await GotoAsync(page, "/spaces/CBU/stages");
        await Stage(page, "CBU-1").GetByTestId("request-approval").ClickAsync();
        var dialog = page.GetByTestId("request-dialog");
        await Assertions.Expect(dialog).ToContainTextAsync("Write the charter");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Request approval" }).ClickAsync();
        await Assertions.Expect(Stage(page, "CBU-1").GetByTestId("gate-state")).ToHaveTextAsync("Awaiting decision");
    }

    [Fact]
    public async Task P3_US3_Independent_test_a_gate_is_sent_back_then_approved_and_the_next_stage_unlocks()
    {
        var saraPassword = await App.CreateUserAsync("sara.p3", "Sara Malik");
        var aminaPassword = await App.CreateUserAsync("amina.g3", "Amina Khan");
        var omarPassword = await App.CreateUserAsync("omar.p3", "Omar Gatekeeper");
        var farahPassword = await App.CreateUserAsync("farah.p3", "Farah Sponsor");

        // sara, the Project Admin, creates CBU with its four stages; amina and omar are Members.
        var sara = await SignInAsync("sara.p3", saraPassword);
        await GotoAsync(sara, "/spaces");
        await sara.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = sara.GetByTestId("create-project");
        await create.GetByLabel("Stage-gate", new() { Exact = true }).CheckAsync();
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Core Banking Upgrade");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("CBU");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/CBU/board");
        await App.AddMemberAsync("CBU", "amina.g3");
        await App.AddMemberAsync("CBU", "omar.p3");
        await AddToStageAsync(sara, "CBU-1", "Write the charter");     // CBU-5
        await AddToStageAsync(sara, "CBU-2", "Plan the migration");    // CBU-6

        // P3_US3_AS1: farah (outside the project) and omar are named; farah becomes a Viewer.
        await GotoAsync(sara, "/spaces/CBU/stages");
        await Assertions.Expect(sara.GetByTestId("stage-place")).ToHaveTextAsync(["Current", "Locked", "Locked", "Locked"]);
        await Stage(sara, "CBU-1").GetByTestId("set-up-gate").ClickAsync();
        var setup = sara.GetByTestId("gate-setup-dialog");
        foreach (var name in new[] { "Farah Sponsor", "Omar Gatekeeper" })
        {
            await setup.Locator("#approver-search").FillAsync(name.Split(' ')[0]);
            await setup.GetByTestId("person-result").Filter(new() { HasText = name }).ClickAsync();
        }

        await sara.AssertNoAccessibilityViolationsAsync();
        await setup.GetByRole(AriaRole.Button, new() { Name = "Save gate" }).ClickAsync();
        await Assertions.Expect(Stage(sara, "CBU-1").GetByTestId("gate-approver")).ToHaveCountAsync(2);
        await GotoAsync(sara, "/spaces/CBU/members");
        await Assertions.Expect(sara.Locator("tr", new() { HasText = "Farah Sponsor" })).ToContainTextAsync("Viewer");

        // P3_US3_AS2: work in locked Planning cannot start.
        var amina = await SignInAsync("amina.g3", aminaPassword);
        await GotoAsync(amina, "/spaces/CBU/board");
        var card = amina.GetByTestId("card-CBU-6");
        await card.Locator(".move-btn").ClickAsync();
        await card.GetByRole(AriaRole.Button, new() { Name = "In Progress: bottom" }).ClickAsync();
        await Assertions.Expect(amina.Locator(".alert-error")).ToContainTextAsync("Initiation gate");

        // P3_US3_AS3: amina requests approval, warned about the open task; P3_US3_AS4–AS5: farah approves, omar sends it back.
        await RequestAsync(amina);
        var farah = await SignInAsync("farah.p3", farahPassword);
        await GotoAsync(farah, "/my-tasks");
        await farah.AssertNoAccessibilityViolationsAsync();
        await DecideAsync(farah, "Approve");
        await Assertions.Expect(Stage(farah, "CBU-1").GetByTestId("gate-state")).ToHaveTextAsync("Awaiting decision");
        var omar = await SignInAsync("omar.p3", omarPassword);
        await GotoAsync(omar, "/my-tasks");
        await omar.GetByTestId("gates-waiting").GetByRole(AriaRole.Link, new() { Name = "Initiation gate" }).ClickAsync();
        await WaitForPathAsync(omar, "/spaces/CBU/stages?stage=CBU-1");
        await WaitForInteractivityAsync(omar);
        await Stage(omar, "CBU-1").GetByTestId("send-back-gate").ClickAsync();
        await omar.AssertNoAccessibilityViolationsAsync();
        var decision = omar.GetByTestId("decision-dialog");
        await decision.Locator("#gate-note").FillAsync("The budget is missing.");
        await decision.GetByRole(AriaRole.Button, new() { Name = "Send back", Exact = true }).ClickAsync();
        await Assertions.Expect(Stage(omar, "CBU-1").GetByTestId("gate-state")).ToHaveTextAsync("Sent back");
        await Assertions.Expect(Stage(omar, "CBU-1").GetByTestId("gate-note")).ToContainTextAsync("The budget is missing.");

        // Requested again; both approve; Planning unlocks and its task can start.
        await RequestAsync(amina);
        await DecideAsync(farah, "Approve");
        await DecideAsync(omar, "Approve");
        await Assertions.Expect(Stage(omar, "CBU-1").GetByTestId("gate-state")).ToHaveTextAsync("Approved");
        await Assertions.Expect(omar.GetByTestId("stage-place")).ToHaveTextAsync(["Approved", "Current", "Locked", "Locked"]);
        await omar.AssertNoAccessibilityViolationsAsync();
        await omar.SetViewportSizeAsync(360, 740);
        await omar.AssertNoAccessibilityViolationsAsync();

        await GotoAsync(amina, "/spaces/CBU/board");
        card = amina.GetByTestId("card-CBU-6");
        await card.Locator(".move-btn").ClickAsync();
        await card.GetByRole(AriaRole.Button, new() { Name = "In Progress: bottom" }).ClickAsync();
        await Assertions.Expect(amina.Locator("[data-testid=column]").Nth(1).GetByTestId("card-CBU-6")).ToBeVisibleAsync();

        // P3_US3_AS7 and AS10: Initiation can be completed, and its history shows each step.
        await GotoAsync(sara, "/spaces/CBU/board?task=CBU-1");
        await Drawer(sara).Locator("#drawer-status").SelectOptionAsync(new SelectOptionValue { Label = "Completed" });
        await Assertions.Expect(Drawer(sara).GetByTestId("drawer-saved")).ToContainTextAsync("Status changed to Completed");
        await Drawer(sara).Locator("summary", new() { HasText = "History" }).ClickAsync();
        var history = Drawer(sara).GetByTestId("history-entry");
        await Assertions.Expect(history.Filter(new() { HasText = "set up the gate" })).ToHaveCountAsync(1);
        await Assertions.Expect(history.Filter(new() { HasText = "requested approval of the gate" })).ToHaveCountAsync(2);
        await Assertions.Expect(history.Filter(new() { HasText = "sent the gate back: “The budget is missing.”" })).ToHaveCountAsync(1);
        await Assertions.Expect(history.Filter(new() { HasText = "the gate is approved" })).ToHaveCountAsync(1);
    }
}
