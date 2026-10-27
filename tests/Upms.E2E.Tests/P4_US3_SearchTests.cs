using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 4 User Story 3 end to end: a key jump from the search box, recent items, a word search across the
/// projects one can see and none other, filters, a saved filter run again and shared with a project; with axe scans of
/// the search box, the search page and the saved-filter panel at 1280 px and 360 px.</summary>
public sealed class P4_US3_SearchTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Box(IPage page) => page.Locator("#global-search");

    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static ILocator Panel(IPage page) => page.GetByTestId("saved-filters");

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

    private static async Task<IReadOnlyList<string>> ResultKeysAsync(IPage page) =>
        await page.GetByTestId("search-result").Locator(".key").AllTextContentsAsync();

    [Fact]
    public async Task P4_US3_Independent_test_find_filter_and_save_a_search()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p4u3", "Amina Search");
        var bilalPassword = await App.CreateUserAsync("bilal.p4u3", "Bilal Search");
        var saraPassword = await App.CreateUserAsync("sara.p4u3", "Sara Search");
        var amina = await SignInAsync("amina.p4u3", aminaPassword);
        await CreateProjectAsync(amina, "Search Website", "SWEB");
        await CreateProjectAsync(amina, "Search Mobile", "SMOB");
        await App.AddMemberAsync("SWEB", "bilal.p4u3");
        var sara = await SignInAsync("sara.p4u3", saraPassword);
        await CreateProjectAsync(sara, "Search Payments", "SPAY");
        await App.AddTasksAsync("SPAY", [("Refund limits for cards", null, null)]);
        await App.AddTasksAsync("SWEB", [("Refund to card", null, null), ("Refunds report", null, "amina.p4u3")]);
        await App.AddTasksAsync("SMOB", Enumerable.Range(1, 11).Select(n => ($"Mobile task {n}", (DateOnly?)null, (string?)null))
            .Append(("Refund history screen", null, null)));

        // P4_US3_AS1: "smob-12" and Enter open SMOB-12.
        await GotoAsync(amina, "/spaces");
        await Box(amina).FillAsync("smob-12");
        await Box(amina).PressAsync("Enter");
        await WaitForPathAsync(amina, "/spaces/SMOB/board?task=SMOB-12");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-key")).ToHaveTextAsync("SMOB-12");

        // P4_US3_AS2: a key in a project Amina cannot see gets the same answer as one that does not exist.
        await amina.Keyboard.PressAsync("Escape");
        await Box(amina).FillAsync("SPAY-1");
        await Box(amina).PressAsync("Enter");
        await Assertions.Expect(amina.Locator("#global-search-message")).ToHaveTextAsync("No work item SPAY-1 was found.");
        await Box(amina).FillAsync("SMOB-99");
        await Box(amina).PressAsync("Enter");
        await Assertions.Expect(amina.Locator("#global-search-message")).ToHaveTextAsync("No work item SMOB-99 was found.");

        // P4_US3_AS3: the empty box lists what Amina opened, to open with the keyboard; axe at 1280 px and 360 px.
        await GotoAsync(amina, "/spaces/SWEB/board?task=SWEB-1");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-key")).ToHaveTextAsync("SWEB-1");
        await Drawer(amina).Locator("#drawer-priority").SelectOptionAsync("High");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Priority changed to High");
        await amina.Keyboard.PressAsync("Escape");
        await Box(amina).FocusAsync();
        var recent = amina.Locator("#global-search-list [role=option]");
        await Assertions.Expect(recent.Locator(".key")).ToHaveTextAsync(["SWEB-1", "SMOB-12"]);
        await amina.AssertNoAccessibilityViolationsAsync();
        await Box(amina).PressAsync("ArrowDown");
        await Box(amina).PressAsync("ArrowDown");
        await Box(amina).PressAsync("Enter");
        await WaitForPathAsync(amina, "/spaces/SMOB/board?task=SMOB-12");

        // P4_US3_AS4 and AS7: "refund" finds SWEB and SMOB items, latest first, and nothing from SPAY.
        await amina.Keyboard.PressAsync("Escape");
        await Box(amina).FillAsync("refund");
        await Box(amina).PressAsync("Enter");
        await WaitForPathAsync(amina, "/search?q=refund");
        await Assertions.Expect(amina.GetByTestId("result-count")).ToHaveTextAsync("3 results");
        Assert.Equal(["SWEB-1", "SMOB-12", "SWEB-2"], await ResultKeysAsync(amina));

        // P4_US3_AS5: status type "To do", unassigned and priority "High" narrow it to SWEB-1, as removable chips.
        await amina.Locator("#filter-category").SelectOptionAsync("todo");
        await Assertions.Expect(amina.GetByTestId("search-chips")).ToContainTextAsync("Status type: To do");
        await amina.Locator("#filter-assignee").SelectOptionAsync("none");
        await Assertions.Expect(amina.GetByTestId("search-chips")).ToContainTextAsync("Unassigned");
        await amina.Locator("#filter-priority").SelectOptionAsync("high");
        await Assertions.Expect(amina.GetByTestId("result-count")).ToHaveTextAsync("1 result");
        Assert.Equal(["SWEB-1"], await ResultKeysAsync(amina));
        await Assertions.Expect(amina.GetByTestId("search-chips").Locator(".chip")).ToHaveCountAsync(3);

        // P4_US3_AS8: saved as "Unassigned refunds", listed among Amina's filters.
        await Panel(amina).GetByLabel("Save this search as").FillAsync("Unassigned refunds");
        await Panel(amina).GetByRole(AriaRole.Button, new() { Name = "Save as filter" }).ClickAsync();
        await Assertions.Expect(Panel(amina).GetByRole(AriaRole.Link, new() { Name = "Unassigned refunds" })).ToBeVisibleAsync();
        await Assertions.Expect(amina).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("filter=\\d+"));
        await amina.AssertNoAccessibilityViolationsAsync();

        // P4_US3_AS9: a result opens in the drawer over the results; closing it puts focus back on the result.
        await amina.Locator("#search-result-SWEB-1").ClickAsync();
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-key")).ToHaveTextAsync("SWEB-1");
        await amina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(amina.Locator("#search-result-SWEB-1")).ToBeFocusedAsync();

        // Amina takes SWEB-1 from the drawer: it no longer matches "Unassigned", so it leaves the results.
        await amina.Locator("#search-result-SWEB-1").ClickAsync();
        await Drawer(amina).GetByTestId("assign-to-me").ClickAsync();
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Assigned to you");
        await amina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(amina.GetByTestId("result-count")).ToHaveTextAsync("0 results");

        // Run again later, the filter shows the current results: SWEB-1 is no longer unassigned.
        await GotoAsync(amina, "/search");
        await Panel(amina).GetByRole(AriaRole.Link, new() { Name = "Unassigned refunds" }).ClickAsync();
        await Assertions.Expect(amina.GetByTestId("result-count")).ToHaveTextAsync("0 results");
        await Assertions.Expect(amina.Locator("#search-words")).ToHaveValueAsync("refund");

        // P4_US3_AS10: shared with SWEB, Bilal finds it with Amina's name, runs it and cannot change it.
        await Panel(amina).GetByRole(AriaRole.Button, new() { Name = "Share" }).ClickAsync();
        await Panel(amina).GetByLabel("SWEB – Search Website").CheckAsync();
        await Panel(amina).GetByRole(AriaRole.Button, new() { Name = "Save sharing" }).ClickAsync();
        await Assertions.Expect(Panel(amina)).ToContainTextAsync("shared with SWEB");

        var bilal = await SignInAsync("bilal.p4u3", bilalPassword);
        await GotoAsync(bilal, "/search");
        var shared = Panel(bilal).GetByTestId("shared-filters");
        await Assertions.Expect(shared).ToContainTextAsync("Unassigned refunds by Amina Search");
        await shared.GetByRole(AriaRole.Link, new() { Name = "Unassigned refunds" }).ClickAsync();
        await Assertions.Expect(bilal.Locator("#search-words")).ToHaveValueAsync("refund");
        await Assertions.Expect(Panel(bilal).GetByRole(AriaRole.Button, new() { Name = "Rename" })).ToHaveCountAsync(0);
        await Assertions.Expect(Panel(bilal).GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToHaveCountAsync(0);

        // The search page and the panel at 360 px.
        await bilal.SetViewportSizeAsync(360, 740);
        await GotoAsync(bilal, "/search?q=refund");
        await Assertions.Expect(bilal.GetByTestId("result-count")).ToHaveTextAsync("2 results");
        await bilal.AssertNoAccessibilityViolationsAsync();
        await Box(bilal).FocusAsync();
        await bilal.AssertNoAccessibilityViolationsAsync();
    }
}
