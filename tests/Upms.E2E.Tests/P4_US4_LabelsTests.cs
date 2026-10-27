using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 4 User Story 4 end to end: a label added on one project is suggested on another, both items are found by
/// it, and the board's quick filters for Bug and an epic leave only matching cards with column counts; a Viewer sees
/// labels and filters but cannot change labels; with axe scans of the label combobox and a quick-filtered board.</summary>
public sealed class P4_US4_LabelsTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static ILocator Labels(IPage page) => Drawer(page).GetByTestId("labels");

    private static async Task CreateProjectAsync(IPage page, string name, string key)
    {
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync(name);
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, $"/spaces/{key}/board");
    }

    private static async Task<IReadOnlyList<string>> CardKeysAsync(IPage page) =>
        await page.Locator("article.bcard .key").AllTextContentsAsync();

    [Fact]
    public async Task P4_US4_Independent_test_label_find_and_quick_filter()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p4u4", "Amina Labels");
        var bilalPassword = await App.CreateUserAsync("bilal.p4u4", "Bilal Viewer");
        var amina = await SignInAsync("amina.p4u4", aminaPassword);
        await CreateProjectAsync(amina, "Labels Website", "LWEB");
        await CreateProjectAsync(amina, "Labels Mobile", "LMOB");
        await App.AddMemberAsync("LMOB", "bilal.p4u4", ProjectRole.Viewer);
        await App.AddTasksAsync("LWEB", [("Plan the launch", null, null)]);
        await App.AddPlanAsync("LMOB",
        [
            (WorkItemType.Epic, "Card refunds", null, null, null),          // LMOB-1
            (WorkItemType.Bug, "Refund fails", "LMOB-1", null, null),       // LMOB-2
            (WorkItemType.Story, "Refund history", "LMOB-1", null, null),   // LMOB-3
            (WorkItemType.Bug, "Sign-up crash", null, null, null),          // LMOB-4
        ]);

        // P4_US4_AS1: "launch" added to LWEB-1 with Enter.
        await GotoAsync(amina, "/spaces/LWEB/board?task=LWEB-1");
        await Labels(amina).GetByRole(AriaRole.Combobox, new() { Name = "Add label" }).FillAsync("launch");
        await Labels(amina).GetByRole(AriaRole.Combobox, new() { Name = "Add label" }).PressAsync("Enter");
        await Assertions.Expect(Labels(amina).Locator(".label-chip")).ToHaveTextAsync([new System.Text.RegularExpressions.Regex("^\\s*launch")]);
        await Assertions.Expect(amina.GetByTestId("card-LWEB-1").GetByTestId("card-labels")).ToHaveTextAsync("launch");

        // P4_US4_AS2 and AS3: on LMOB-3, "lau" suggests it, and the first spelling is kept.
        await GotoAsync(amina, "/spaces/LMOB/board?task=LMOB-3");
        var input = Labels(amina).GetByRole(AriaRole.Combobox, new() { Name = "Add label" });
        await input.PressSequentiallyAsync("lau");
        await Assertions.Expect(amina.Locator("#label-suggestions [role=option]")).ToHaveTextAsync(["launch"]);
        await amina.AssertNoAccessibilityViolationsAsync();
        await input.PressAsync("ArrowDown");
        await input.PressAsync("Enter");
        await Assertions.Expect(Labels(amina).GetByRole(AriaRole.Button, new() { Name = "Remove label launch" })).ToBeVisibleAsync();
        await amina.Keyboard.PressAsync("Escape");

        // P4_US4_AS4 and AS6: both items are found by the label.
        await GotoAsync(amina, "/search?label=Launch");
        await Assertions.Expect(amina.GetByTestId("result-count")).ToHaveTextAsync("2 results");
        await Assertions.Expect(amina.GetByTestId("search-result").Filter(new() { HasText = "LWEB-1" })).ToContainTextAsync("launch");
        await GotoAsync(amina, "/spaces/LMOB/list?labels=launch");
        await Assertions.Expect(amina.GetByTestId("list-row")).ToHaveCountAsync(1);

        // P4_US4_AS7: Bug and the epic "Card refunds" leave LMOB-2, with column counts, kept in the address.
        await GotoAsync(amina, "/spaces/LMOB/board");
        await Assertions.Expect(amina.Locator("article.bcard")).ToHaveCountAsync(3);
        Assert.Equal(["LMOB-2", "LMOB-3", "LMOB-4"], (await CardKeysAsync(amina)).Order());
        await amina.GetByTestId("quick-kind-bug").ClickAsync();
        await amina.GetByTestId("quick-container-LMOB-1").ClickAsync();
        await WaitForPathAsync(amina, "/spaces/LMOB/board?kinds=bug&containers=LMOB-1");
        await Assertions.Expect(amina.Locator("article.bcard")).ToHaveCountAsync(1);
        Assert.Equal(["LMOB-2"], await CardKeysAsync(amina));
        await Assertions.Expect(amina.GetByTestId("column").First.Locator(".count")).ToHaveTextAsync("1 of 3");
        await Assertions.Expect(amina.GetByTestId("quick-kind-bug")).ToHaveAttributeAsync("aria-pressed", "true");
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(360, 740);
        await amina.AssertNoAccessibilityViolationsAsync();

        // P4_US4_AS8: Bilal, a Viewer, sees the label and can filter, but cannot add or remove labels.
        var bilal = await SignInAsync("bilal.p4u4", bilalPassword);
        await GotoAsync(bilal, "/spaces/LMOB/board?labels=launch&task=LMOB-3");
        await Assertions.Expect(bilal.Locator("article.bcard")).ToHaveCountAsync(1);
        Assert.Equal(["LMOB-3"], await CardKeysAsync(bilal));
        await Assertions.Expect(Labels(bilal).Locator(".label-chip")).ToHaveTextAsync(["launch"]);
        await Assertions.Expect(Labels(bilal).GetByRole(AriaRole.Combobox)).ToHaveCountAsync(0);
        await Assertions.Expect(Labels(bilal).GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
    }
}
