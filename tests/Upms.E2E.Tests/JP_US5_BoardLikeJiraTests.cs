using Microsoft.Playwright;
using Upms.Domain.Work;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Jira parity pass User Story 5 end to end: Jira-style cards with colored epic tags, a sub-task as its own card,
/// swimlanes by epic with a drag into another epic's lane, a keyboard "Move to › Lane" reassignment, and an epic's color
/// changed in its drawer; with axe scans at both widths in the light and dark themes.</summary>
public sealed class JP_US5_BoardLikeJiraTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Lane(IPage page, string title) =>
        page.GetByTestId("lane").Filter(new() { Has = page.Locator(".lane-title", new() { HasTextString = title }) });

    private static ILocator Cell(IPage page, string lane, int column) =>
        Lane(page, lane).Locator(".lane-cell").Nth(column);

    private static async Task ScanBothThemesAsync(IPage page)
    {
        await page.AssertNoAccessibilityViolationsAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.AssertNoAccessibilityViolationsAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
    }

    [Fact]
    public async Task JP_US5_Independent_test_cards_lanes_and_lane_moves()
    {
        var password = await App.CreateUserAsync("sara.jp5", "Sara Lanes");
        var sara = await SignInAsync("sara.jp5", password);
        await GotoAsync(sara, "/spaces");
        await sara.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = sara.GetByTestId("create-project");
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Lane Board");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("LNB");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/LNB/board");
        await App.AddPlanAsync("LNB",
        [
            (WorkItemType.Epic, "Card refunds", null, null, null),       // LNB-1, Purple
            (WorkItemType.Epic, "Wallet", null, null, null),             // LNB-2, Blue
            (WorkItemType.Story, "Refund to card", "LNB-1", null, null), // LNB-3
            (WorkItemType.Story, "Top up", "LNB-2", null, null),         // LNB-4
        ]);

        // JP_US5_AS6: a sub-task added in the drawer is its own card, naming its parent.
        await GotoAsync(sara, "/spaces/LNB/board?task=LNB-3");
        await sara.GetByTestId("drawer").Locator("#add-subtask").FillAsync("Write the refund API");
        await sara.GetByTestId("drawer").Locator("#add-subtask").PressAsync("Enter");
        await Assertions.Expect(sara.GetByTestId("drawer").GetByTestId("subtask")).ToHaveCountAsync(1);
        await sara.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sara.GetByTestId("card-LNB-5").GetByTestId("card-parent")).ToHaveTextAsync("Sub-task of LNB-3");

        // JP_US5_AS1: the card's epic tag in its color.
        var tag = sara.GetByTestId("card-LNB-3").GetByTestId("epic-tag");
        await Assertions.Expect(tag).ToContainTextAsync("Card refunds");
        await Assertions.Expect(tag).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("epic-purple"));
        await ScanBothThemesAsync(sara);

        // JP_US5_AS3: lanes by epic, kept in the address.
        await sara.Locator("#board-group").SelectOptionAsync("epic");
        await WaitForPathAsync(sara, "/spaces/LNB/board?group=epic");
        await Assertions.Expect(sara.GetByTestId("lane").Locator(".lane-title")).ToHaveTextAsync(["LNB-1 Card refunds", "LNB-2 Wallet"]);
        await Assertions.Expect(Lane(sara, "LNB-1 Card refunds").Locator("article.bcard")).ToHaveCountAsync(2);
        await ScanBothThemesAsync(sara);

        // JP_US5_AS5: dragged into the other epic's lane and the In Progress column, it changes epic and status together.
        await sara.GetByTestId("card-LNB-4").DragToAsync(Cell(sara, "LNB-1 Card refunds", 1).GetByTestId("column-drop-end"));
        await Assertions.Expect(Cell(sara, "LNB-1 Card refunds", 1).GetByTestId("card-LNB-4")).ToBeVisibleAsync();
        await Assertions.Expect(sara.GetByTestId("card-LNB-4").GetByTestId("epic-tag")).ToContainTextAsync("Card refunds");

        // JP_US5_AS5 with the keyboard: "Move to › Lane" gives the card to a person.
        await sara.Locator("#board-group").SelectOptionAsync("assignee");
        await WaitForPathAsync(sara, "/spaces/LNB/board?group=assignee");
        await Assertions.Expect(sara.GetByTestId("lane").Locator(".lane-title")).ToHaveTextAsync(["Unassigned"]);
        await sara.GetByTestId("card-LNB-3").Locator(".move-btn").ClickAsync();
        await Assertions.Expect(sara.GetByTestId("card-LNB-3").Locator(".lane-option")).ToHaveCountAsync(0);
        await sara.Keyboard.PressAsync("Escape");
        await GotoAsync(sara, "/spaces/LNB/board?group=assignee&task=LNB-3");
        await sara.GetByTestId("drawer").Locator("#drawer-assignee").SelectOptionAsync(new SelectOptionValue { Label = "Sara Lanes (me)" });
        await Assertions.Expect(sara.GetByTestId("drawer").GetByTestId("assign-to-me")).ToHaveCountAsync(0);
        await sara.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sara.GetByTestId("lane").Locator(".lane-title")).ToHaveTextAsync(["Sara Lanes", "Unassigned"]);
        await sara.GetByTestId("card-LNB-4").Locator(".move-btn").FocusAsync();
        await sara.Keyboard.PressAsync("Enter");
        await sara.GetByTestId("card-LNB-4").Locator(".lane-option", new() { HasTextString = "Lane: Sara Lanes" }).FocusAsync();
        await sara.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Lane(sara, "Sara Lanes").GetByTestId("card-LNB-4")).ToBeVisibleAsync();

        // JP_US5_AS2: an epic's color, changed in its drawer, is its tag's color everywhere.
        await GotoAsync(sara, "/spaces/LNB/board?task=LNB-1");
        await sara.GetByTestId("drawer").GetByRole(AriaRole.Radio, new() { Name = "Teal" }).CheckAsync();
        await Assertions.Expect(sara.GetByTestId("drawer").GetByRole(AriaRole.Radio, new() { Name = "Teal" })).ToBeCheckedAsync();
        await sara.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sara.GetByTestId("card-LNB-3").GetByTestId("epic-tag")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("epic-teal"));

        // At 360 px the lanes and cards still read and work.
        await sara.SetViewportSizeAsync(360, 740);
        await GotoAsync(sara, "/spaces/LNB/board?group=epic");
        await ScanBothThemesAsync(sara);
    }
}
