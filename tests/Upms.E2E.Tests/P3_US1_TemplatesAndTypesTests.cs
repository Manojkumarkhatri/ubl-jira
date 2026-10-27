using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 3 User Story 1 end to end: a template per project, and work organized by type, epic and stage.</summary>
public sealed partial class P3_US1_TemplatesAndTypesTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Drawer(IPage page) => page.GetByTestId("drawer");

    private static Task<string[]> KeysAsync(IPage page) =>
        page.GetByTestId("list-row").EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.key)");

    [GeneratedRegex(@"/spaces/TPM/list\?.*kind=story")]
    private static partial Regex StoryFilter();

    [GeneratedRegex(@"/spaces/TPM/list\?.*container=TPM-1")]
    private static partial Regex EpicFilter();

    private static async Task CreateProjectAsync(IPage page, string name, string key, string? template)
    {
        await GotoAsync(page, "/spaces");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = page.GetByTestId("create-project");
        if (template is not null)
        {
            await dialog.GetByLabel(template, new() { Exact = true }).CheckAsync();
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await dialog.GetByLabel("Name").FillAsync(name);
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync(key);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(page, $"/spaces/{key}/board");
        await WaitForInteractivityAsync(page);
    }

    /// <summary>Types a title in the list's "What needs to be done?" box with the chosen type.</summary>
    private static async Task CreateInListAsync(IPage page, string type, string title, string expectedKey)
    {
        var create = page.Locator(".list-create");
        await create.Locator("select.type-choice").SelectOptionAsync(type);
        var box = create.GetByPlaceholder("What needs to be done?");
        await box.FillAsync(title);
        await box.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("list-row").First).ToHaveAttributeAsync("data-key", expectedKey);
    }

    private static async Task OpenFromListAsync(IPage page, string key)
    {
        await page.Locator($"[data-key='{key}'] a.task-link").ClickAsync();
        await Assertions.Expect(Drawer(page).GetByTestId("drawer-key")).ToHaveTextAsync(key);
    }

    private static async Task CloseDrawerAsync(IPage page)
    {
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Drawer(page)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task P3_US1_Independent_test_templates_types_epics_stages_and_milestones()
    {
        var aminaPassword = await App.CreateUserAsync("amina.p3", "Amina Khan");
        var amina = await SignInAsync("amina.p3", aminaPassword);

        // P3_US1_AS1, with the Jira parity pass's gallery: four templates (Blank added), each with one line, and Kanban preselected.
        await GotoAsync(amina, "/spaces");
        await amina.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = amina.GetByTestId("create-project");
        await Assertions.Expect(dialog.GetByRole(AriaRole.Radio)).ToHaveCountAsync(4);
        await Assertions.Expect(dialog.GetByLabel("Kanban", new() { Exact = true })).ToBeCheckedAsync();
        await Assertions.Expect(dialog.Locator(".template-choice .hint")).ToHaveCountAsync(4);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.Keyboard.PressAsync("Escape");

        // A Kanban project, as every project was before: its board creates stories, tasks and bugs, each with its symbol.
        await CreateProjectAsync(amina, "Web Portal", "TPW", template: null);
        await amina.Locator("[data-testid=column]").First.Locator("select.type-choice").SelectOptionAsync("Bug");
        var boardBox = amina.Locator("[data-testid=column]").First.GetByPlaceholder("What needs to be done?");
        await boardBox.FillAsync("Login fails on Safari");
        await boardBox.PressAsync("Enter");
        await Assertions.Expect(amina.GetByTestId("card-TPW-1").Locator(".type-icon .sr-only")).ToHaveTextAsync("Bug");
        await amina.AssertNoAccessibilityViolationsAsync();

        // P3_US1_AS2: the columns of each template, and the four stages of a Stage-gate project.
        // A Scrum board shows only an active sprint (P3 FR-027), so its columns are read from the list's status filter.
        await CreateProjectAsync(amina, "Mobile Portal", "TPM", "Scrum");
        await GotoAsync(amina, "/spaces/TPM/list");
        await Assertions.Expect(amina.Locator("#filter-column option")).ToHaveTextAsync(["Any", "To Do", "In Progress", "In Review", "Done"]);
        await CreateProjectAsync(amina, "Core Banking Pilot", "TPC", "Stage-gate");
        await Assertions.Expect(amina.Locator("[data-testid=column] h2")).ToHaveTextAsync(["Not Started", "In Progress", "On Hold", "Completed"]);
        await GotoAsync(amina, "/spaces/TPC/list?sort=key&dir=asc");
        await Assertions.Expect(amina.Locator("[data-testid=list-row] a.task-link")).ToHaveTextAsync(["Initiation", "Planning", "Execution", "Closure"]);
        await Assertions.Expect(amina.Locator("[data-testid=list-row] .type-icon .sr-only")).ToHaveTextAsync(["Stage", "Stage", "Stage", "Stage"]);

        // P3_US1_AS4: an epic from the list is listed but is not a card on the board.
        await GotoAsync(amina, "/spaces/TPM/list");
        await CreateInListAsync(amina, "Epic", "Card refunds", "TPM-1");
        await CreateInListAsync(amina, "Story", "Refund to card", "TPM-2");
        await CreateInListAsync(amina, "Bug", "Refund shows twice", "TPM-3");
        await Assertions.Expect(amina.Locator("[data-key='TPM-3'] .type-icon .sr-only")).ToHaveTextAsync("Bug");

        // P3_US1_AS5 and AS6: both go into the epic from their drawers; the bug becomes a story, which its history records.
        await OpenFromListAsync(amina, "TPM-3");
        await Drawer(amina).Locator("#drawer-container").SelectOptionAsync("TPM-1");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Put in TPM-1");
        await Drawer(amina).Locator("#drawer-type").SelectOptionAsync("Story");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Type changed to Story");
        await Drawer(amina).Locator("summary", new() { HasText = "History" }).ClickAsync();
        await Assertions.Expect(Drawer(amina).GetByTestId("history-entry").Last).ToContainTextAsync("changed the type from Bug to Story");
        await CloseDrawerAsync(amina);

        await OpenFromListAsync(amina, "TPM-2");
        await Drawer(amina).Locator("#drawer-container").SelectOptionAsync("TPM-1");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Put in TPM-1");
        await Drawer(amina).Locator("#drawer-estimate").FillAsync("3");
        await Drawer(amina).Locator("#drawer-estimate").PressAsync("Tab");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Estimate saved");
        await Drawer(amina).Locator("#drawer-status").SelectOptionAsync(new SelectOptionValue { Label = "Done" });
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Status changed to Done");
        await CloseDrawerAsync(amina);

        await OpenFromListAsync(amina, "TPM-1");
        var items = Drawer(amina).GetByTestId("container-items");
        await Assertions.Expect(items).ToContainTextAsync("1 of 2 done (50%)");
        await Assertions.Expect(items.GetByTestId("contained-key")).ToHaveTextAsync(["TPM-2", "TPM-3"]);
        await Assertions.Expect(Drawer(amina).Locator("#drawer-type")).ToHaveCountAsync(0);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(360, 740);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(1280, 900);
        await CloseDrawerAsync(amina);

        // P3_US1_AS8: filter by type and epic; each row shows both, and the address keeps them.
        await amina.Locator("#filter-kind").SelectOptionAsync("story");
        await Assertions.Expect(amina).ToHaveURLAsync(StoryFilter());
        await amina.Locator("#filter-container").SelectOptionAsync("TPM-1");
        await Assertions.Expect(amina).ToHaveURLAsync(EpicFilter());
        await Assertions.Expect(amina.GetByTestId("list-row")).ToHaveCountAsync(2);
        Assert.Equal(["TPM-3", "TPM-2"], await KeysAsync(amina));
        await Assertions.Expect(amina.Locator("[data-key='TPM-3'] [data-testid=row-container]")).ToContainTextAsync("TPM-1 Card refunds");
        await Assertions.Expect(amina.Locator("[data-testid=filter-chip] .chip-label")).ToHaveTextAsync(["Type: Story", "Epic: TPM-1 Card refunds"]);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(360, 740);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(1280, 900);

        // The epic is not a card on the board.
        await GotoAsync(amina, "/spaces/TPM/board");
        await Assertions.Expect(amina.GetByTestId("card-TPM-1")).ToHaveCountAsync(0);

        // P3_US1_AS7: a milestone and a task created in the stage Planning; the milestone has one date and no sub-tasks,
        // and is not a card on the board.
        await GotoAsync(amina, "/spaces/TPC/list");
        await OpenFromListAsync(amina, "TPC-2");
        var stageItems = Drawer(amina).GetByTestId("container-items");
        await stageItems.Locator("#container-add-type").SelectOptionAsync("Milestone");
        await stageItems.Locator("#container-add-input").FillAsync("Planning sign-off");
        await stageItems.Locator("#container-add-input").PressAsync("Enter");
        await Assertions.Expect(stageItems.GetByTestId("contained-key")).ToHaveTextAsync(["TPC-5"]);
        await stageItems.Locator("#container-add-type").SelectOptionAsync("Task");
        await stageItems.Locator("#container-add-input").FillAsync("Vendor shortlist");
        await stageItems.Locator("#container-add-input").PressAsync("Enter");
        await Assertions.Expect(stageItems.GetByTestId("contained-key")).ToHaveTextAsync(["TPC-5", "TPC-6"]);
        await Assertions.Expect(stageItems).ToContainTextAsync("0 of 1 done (0%)");

        await stageItems.GetByRole(AriaRole.Link, new() { Name = "TPC-5 Planning sign-off" }).ClickAsync();
        await WaitForPathAsync(amina, "/spaces/TPC/board?task=TPC-5");
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-key")).ToHaveTextAsync("TPC-5");
        await Assertions.Expect(Drawer(amina).Locator("#drawer-start")).ToHaveCountAsync(0);
        await Assertions.Expect(Drawer(amina).Locator("#add-subtask")).ToHaveCountAsync(0);
        await Assertions.Expect(Drawer(amina).Locator("#drawer-container")).ToHaveValueAsync("TPC-2");
        var signOff = new DateOnly(DateTime.UtcNow.Year + 1, 10, 30);
        await Drawer(amina).Locator("#drawer-due").FillAsync(signOff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await Assertions.Expect(Drawer(amina).GetByTestId("drawer-saved")).ToContainTextAsync("Date saved"); // a milestone has one date, "Date"
        await CloseDrawerAsync(amina);
        await Assertions.Expect(amina.GetByTestId("card-TPC-5")).ToHaveCountAsync(0);
        await Assertions.Expect(amina.GetByTestId("card-TPC-6")).ToHaveCountAsync(1);

        // The project list shows each project's template.
        await GotoAsync(amina, "/spaces");
        await Assertions.Expect(amina.Locator("tbody tr", new() { HasText = "Web Portal" }).GetByTestId("project-template")).ToHaveTextAsync("Kanban");
        await Assertions.Expect(amina.Locator("tbody tr", new() { HasText = "Mobile Portal" }).GetByTestId("project-template")).ToHaveTextAsync("Scrum");
        await Assertions.Expect(amina.Locator("tbody tr", new() { HasText = "Core Banking" }).GetByTestId("project-template")).ToHaveTextAsync("Stage-gate");
    }
}
