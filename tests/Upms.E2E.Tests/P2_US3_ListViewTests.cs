using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 2 User Story 3 end to end: a sortable, filterable list that can be shared by its address.</summary>
public sealed partial class P2_US3_ListViewTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static Task<string[]> KeysAsync(IPage page) =>
        page.GetByTestId("list-row").EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.key)");

    [GeneratedRegex(@"/projects/CAT/list\?sort=due&due=overdue&assignee=none$")]
    private static partial Regex SharedAddress();

    [Fact]
    public async Task P2_US3_Independent_test_sort_filter_and_share_a_list_then_edit_a_row()
    {
        var ilyasPassword = await App.CreateUserAsync("ilyas", "Ilyas Butt");
        var junaidPassword = await App.CreateUserAsync("junaid", "Junaid Aslam");
        var ilyas = await SignInAsync("ilyas", ilyasPassword);
        await GotoAsync(ilyas, "/projects");
        await ilyas.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = ilyas.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Catalogue");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("CAT");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await WaitForPathAsync(ilyas, "/projects/CAT/board");
        await App.AddMemberAsync("CAT", "junaid");
        // Due dates at least two days away from today, so the result is the same in every viewer's time zone.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await App.AddTasksAsync("CAT", Enumerable.Range(1, 60).Select(n => ($"Catalogue item {n}", n switch
        {
            5 => today.AddDays(-3),
            12 => today.AddDays(-2),
            20 => today.AddDays(-5),
            30 => today.AddDays(3),
            _ => (DateOnly?)null,
        }, n == 20 ? "junaid" : null)));

        // P2_US3_AS1: the List view shows the first 50 tasks, newest first, with the total.
        await GotoAsync(ilyas, "/projects/CAT/board");
        await ilyas.Locator("nav.project-nav").GetByRole(AriaRole.Link, new() { Name = "List" }).ClickAsync();
        await WaitForPathAsync(ilyas, "/projects/CAT/list");
        await WaitForInteractivityAsync(ilyas);
        var rows = ilyas.GetByTestId("list-row");
        await Assertions.Expect(rows).ToHaveCountAsync(50);
        await Assertions.Expect(ilyas.GetByTestId("list-count")).ToHaveTextAsync("60 tasks");
        await Assertions.Expect(rows.First).ToHaveAttributeAsync("data-key", "CAT-60");

        // P2_US3_AS6: a task added from the list starts in the first "to do" column.
        var box = ilyas.GetByPlaceholder("What needs to be done?");
        await box.FillAsync("Added from the list");
        await box.PressAsync("Enter");
        await Assertions.Expect(ilyas.GetByTestId("list-count")).ToHaveTextAsync("61 tasks");
        await Assertions.Expect(rows.First).ToHaveAttributeAsync("data-key", "CAT-61");
        await Assertions.Expect(rows.First).ToContainTextAsync("To Do");

        // P2_US3_AS2 and AS3: sorted by due date, only overdue tasks nobody has.
        await ilyas.Locator("th[data-sort=due] button").ClickAsync();
        await Assertions.Expect(ilyas.Locator("th[data-sort=due]")).ToHaveAttributeAsync("aria-sort", "ascending");
        await ilyas.Locator("#filter-due").SelectOptionAsync("overdue");
        await Assertions.Expect(ilyas.GetByTestId("filter-chip")).ToHaveCountAsync(1);
        await ilyas.Locator("#filter-assignee").SelectOptionAsync("none");
        await Assertions.Expect(rows).ToHaveCountAsync(2);
        Assert.Equal(["CAT-5", "CAT-12"], await KeysAsync(ilyas));
        await Assertions.Expect(ilyas.Locator("[data-testid=filter-chip] .chip-label")).ToHaveTextAsync(["Assignee: Unassigned", "Due: Overdue"]);
        await Assertions.Expect(ilyas).ToHaveURLAsync(SharedAddress());
        await Assertions.Expect(ilyas.Locator("[data-key='CAT-5']")).ToContainTextAsync("Overdue");
        await ilyas.AssertNoAccessibilityViolationsAsync();

        // P2_US3_AS4: another member opening the address sees the same list.
        var junaid = await SignInAsync("junaid", junaidPassword);
        await GotoAsync(junaid, new Uri(ilyas.Url).PathAndQuery);
        await Assertions.Expect(junaid.GetByTestId("list-row")).ToHaveCountAsync(2);
        Assert.Equal(["CAT-5", "CAT-12"], await KeysAsync(junaid));
        await Assertions.Expect(junaid.Locator("th[data-sort=due]")).ToHaveAttributeAsync("aria-sort", "ascending");
        await Assertions.Expect(junaid.Locator("#filter-due")).ToHaveValueAsync("overdue");
        await Assertions.Expect(junaid.Locator("#filter-assignee")).ToHaveValueAsync("none");

        // P2_US3_AS5: a row opens the drawer over the list, and a change shows in the row.
        await ilyas.Locator("[data-key='CAT-5'] a.task-link").ClickAsync();
        await Assertions.Expect(Drawer(ilyas).GetByTestId("drawer-key")).ToHaveTextAsync("CAT-5");
        await Drawer(ilyas).Locator("#drawer-priority").SelectOptionAsync("Highest");
        await Assertions.Expect(Drawer(ilyas).GetByTestId("drawer-saved")).ToContainTextAsync("Priority changed to Highest");
        await ilyas.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Drawer(ilyas)).ToBeHiddenAsync();
        await Assertions.Expect(ilyas.Locator("[data-key='CAT-5']")).ToContainTextAsync("Highest");
        await Assertions.Expect(ilyas).ToHaveURLAsync(SharedAddress());

        // P2_US3_AS3: "Clear filters" shows every task again, still sorted by due date.
        await ilyas.GetByTestId("clear-filters").ClickAsync();
        await Assertions.Expect(ilyas.GetByTestId("list-count")).ToHaveTextAsync("61 tasks");
        await Assertions.Expect(ilyas.GetByTestId("filter-chip")).ToHaveCountAsync(0);
        Assert.Equal(["CAT-20", "CAT-5", "CAT-12", "CAT-30"], (await KeysAsync(ilyas)).Take(4));

        // P2_US3_AS7: nothing matches; "Clear filters" is offered. Then narrow screens (FR-041).
        await ilyas.Locator("#filter-text").FillAsync("nothing like this");
        await ilyas.Locator("#filter-text").PressAsync("Enter");
        await Assertions.Expect(ilyas.GetByText("No task matches these filters.")).ToBeVisibleAsync();
        await ilyas.SetViewportSizeAsync(360, 740);
        await ilyas.AssertNoAccessibilityViolationsAsync();
        await ilyas.GetByTestId("empty-clear-filters").ClickAsync();
        await Assertions.Expect(rows).ToHaveCountAsync(50);
        await ilyas.AssertNoAccessibilityViolationsAsync();
    }
}
