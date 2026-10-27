using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Jira parity pass User Story 4 end to end: the sidebar on every page, a star from the Spaces page, the space's tabs
/// and "…" menu, the collapsed sidebar kept after a reload, the phone panel, the Summary page and "For you", an old bookmark,
/// and no page saying "project"; with axe scans at both widths.</summary>
public sealed class JP_US4_FindYourWayTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Sidebar(IPage page) => page.Locator("aside#sidebar");

    private static ILocator Section(IPage page, string name) =>
        Sidebar(page).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Fact]
    public async Task JP_US4_Independent_test_sidebar_tabs_summary_and_for_you()
    {
        var password = await App.CreateUserAsync("sara.jp4", "Sara Navigator");
        var sara = await SignInAsync("sara.jp4", password);
        await GotoAsync(sara, "/spaces");
        await sara.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = sara.GetByTestId("create-project");
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Navigation Web");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("NAV");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/NAV/board");
        await App.AddPlanAsync("NAV",
        [
            (WorkItemType.Epic, "Card refunds", null, null, null),       // NAV-1
            (WorkItemType.Story, "Refund to card", "NAV-1", null, null), // NAV-2
            (WorkItemType.Bug, "Refund shows twice", null, null, null),  // NAV-3
        ]);

        // JP_US4_AS3: the sidebar's sections; JP_US4_AS6: a star from the Spaces page.
        await GotoAsync(sara, "/spaces");
        await Assertions.Expect(Sidebar(sara).Locator(".side-list > li > a, .side-list > li > button"))
            .ToHaveTextAsync(["For you", "Recent", "Starred", "Spaces", "Filters"]);
        await sara.GetByTestId("star-NAV").ClickAsync();
        await Assertions.Expect(sara.GetByTestId("star-NAV")).ToHaveAttributeAsync("aria-pressed", "true");
        await Section(sara, "Starred").ClickAsync();
        await Assertions.Expect(Sidebar(sara).GetByRole(AriaRole.Link, new() { Name = "Navigation Web" }).First).ToBeVisibleAsync();
        await sara.AssertNoAccessibilityViolationsAsync();

        // JP_US4_AS5: the space's tabs and its "…" menu.
        await Sidebar(sara).GetByRole(AriaRole.Link, new() { Name = "Navigation Web" }).First.ClickAsync();
        await WaitForPathAsync(sara, "/spaces/NAV");
        await WaitForInteractivityAsync(sara);
        await Assertions.Expect(sara.Locator("nav.space-tabs a")).ToHaveTextAsync(["Summary", "Board", "List", "Timeline"]);
        await sara.GetByTestId("space-more").ClickAsync();
        await Assertions.Expect(sara.GetByRole(AriaRole.Menuitem)).ToHaveTextAsync(["Members", "Space settings"]);
        await sara.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sara.GetByTestId("space-more")).ToBeFocusedAsync();

        // JP_US4_AS7: the Summary page.
        await sara.Locator("nav.space-tabs").GetByRole(AriaRole.Link, new() { Name = "Summary" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/NAV/summary");
        await Assertions.Expect(sara.GetByTestId("summary-counts")).ToContainTextAsync("3 created in the last 7 days");
        await Assertions.Expect(sara.GetByTestId("types-of-work")).ToContainTextAsync("Bug");
        await Assertions.Expect(sara.GetByTestId("container-progress")).ToContainTextAsync("0 of 1 done");
        await Assertions.Expect(sara.GetByTestId("recent-activity").Locator("li")).Not.ToHaveCountAsync(0);
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.GetByTestId("summary-counts").GetByRole(AriaRole.Link, new() { Name = "3 created in the last 7 days" }).ClickAsync();
        await Assertions.Expect(sara).ToHaveURLAsync(new Regex(@"/search\?.*created="));
        await Assertions.Expect(sara.GetByTestId("result-count")).ToHaveTextAsync("3 results");

        // JP_US4_AS8: "For you" and its tabs.
        await Sidebar(sara).GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true }).ClickAsync();
        await WaitForPathAsync(sara, "/for-you");
        await Assertions.Expect(sara.Locator("nav[aria-label='For you'] a")).ToHaveTextAsync(["Worked on", "Viewed", "Assigned to me", "Starred"]);
        await sara.Locator("nav[aria-label='For you']").GetByRole(AriaRole.Link, new() { Name = "Starred", Exact = true }).ClickAsync();
        await WaitForPathAsync(sara, "/for-you/starred");
        await Assertions.Expect(sara.GetByTestId("starred-spaces")).ToContainTextAsync("Navigation Web");
        await sara.AssertNoAccessibilityViolationsAsync();

        // JP_US4_AS4: the collapsed sidebar stays collapsed after a reload; on a phone it opens as a panel.
        await sara.GetByTestId("sidebar-toggle").ClickAsync();
        await Assertions.Expect(Sidebar(sara)).ToBeHiddenAsync();
        await GotoAsync(sara, "/for-you/starred");
        await Assertions.Expect(Sidebar(sara)).ToBeHiddenAsync();
        await Assertions.Expect(sara.GetByTestId("sidebar-toggle")).ToHaveAttributeAsync("aria-expanded", "false");
        await sara.GetByTestId("sidebar-toggle").ClickAsync();
        await Assertions.Expect(Sidebar(sara)).ToBeVisibleAsync();
        await sara.SetViewportSizeAsync(360, 740);
        await sara.GetByTestId("menu-button").ClickAsync();
        var panel = sara.GetByTestId("sidebar-panel");
        await Assertions.Expect(panel.GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true })).ToBeVisibleAsync();
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.Keyboard.PressAsync("Escape");
        await Assertions.Expect(panel.GetByRole(AriaRole.Link, new() { Name = "For you", Exact = true })).ToHaveCountAsync(0);
        await sara.SetViewportSizeAsync(1280, 900);

        // JP_US4_AS2: an old bookmark opens the same page at its new address.
        await sara.GotoAsync("/projects/NAV/board?task=NAV-2");
        await WaitForPathAsync(sara, "/spaces/NAV/board?task=NAV-2");
        await Assertions.Expect(sara.GetByTestId("drawer")).ToContainTextAsync("Refund to card");

        // JP_US4_AS1: a Viewer's pages say "space" too.
        var viewerPassword = await App.CreateUserAsync("vera.jp4", "Vera Viewer");
        await App.AddMemberAsync("NAV", "vera.jp4", ProjectRole.Viewer);
        var vera = await SignInAsync("vera.jp4", viewerPassword);
        foreach (var path in new[] { "/spaces", "/spaces/NAV/summary", "/spaces/NAV/members", "/for-you", "/for-you/assigned" })
        {
            await GotoAsync(vera, path);
            await vera.AssertSaysSpaceNotProjectAsync();
        }
    }
}
