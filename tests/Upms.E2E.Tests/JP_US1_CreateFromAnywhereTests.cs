using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Jira parity pass User Story 1 end to end: from a page outside any space, the header's Create makes an epic, a
/// story in it with an assignee, priority and label using "Create another", and a sub-task of the story; each appears where
/// it should; a Viewer is not offered Create; with axe scans of the open form at both widths.</summary>
public sealed class JP_US1_CreateFromAnywhereTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Form(IPage page) => page.GetByTestId("create-work");

    private static async Task CreateSpaceAsync(IPage page, string name, string key)
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

    private static async Task OpenCreateAsync(IPage page)
    {
        await page.GetByTestId("create-button").ClickAsync();
        await Assertions.Expect(Form(page).GetByLabel("Summary")).ToBeFocusedAsync();
    }

    private static Task<IReadOnlyList<string>> ChooseTypeAsync(IPage page, string type) =>
        Form(page).GetByLabel("Work type").SelectOptionAsync(new SelectOptionValue { Label = type });

    private static async Task ChooseParentAsync(IPage page, string key)
    {
        await Form(page).Locator("#create-parent").FillAsync(key);
        await Form(page).GetByRole(AriaRole.Option).Filter(new() { HasText = key }).ClickAsync();
        await Assertions.Expect(Form(page).Locator(".parent-chosen")).ToContainTextAsync(key);
    }

    [Fact]
    public async Task JP_US1_Independent_test_epic_story_and_sub_task_from_the_Create_button()
    {
        var aminaPassword = await App.CreateUserAsync("amina.jp1", "Amina Creator");
        await App.CreateUserAsync("omar.jp1", "Omar Builder");
        var veraPassword = await App.CreateUserAsync("vera.jp1", "Vera Viewer");
        var amina = await SignInAsync("amina.jp1", aminaPassword);
        await CreateSpaceAsync(amina, "Create Anywhere", "CAW");
        await App.AddMemberAsync("CAW", "omar.jp1", ProjectRole.Member);
        await App.AddMemberAsync("CAW", "vera.jp1", ProjectRole.Viewer);

        // JP_US1_AS1: from a page outside any space, Create opens on the space with the focus in Summary.
        await GotoAsync(amina, "/my-tasks");
        await OpenCreateAsync(amina);
        await Assertions.Expect(Form(amina).GetByLabel("Space")).ToHaveValueAsync("CAW");
        await amina.AssertNoAccessibilityViolationsAsync();

        // The epic.
        await ChooseTypeAsync(amina, "Epic");
        await Assertions.Expect(Form(amina).Locator("#create-estimate")).ToHaveCountAsync(0);
        await Form(amina).GetByLabel("Summary").FillAsync("Card refunds");
        await Form(amina).GetByTestId("create-submit").ClickAsync();
        await Assertions.Expect(amina.GetByTestId("created-toast")).ToContainTextAsync("CAW-1 created");

        // JP_US1_AS4, AS6 and AS7: a story in the epic, assigned, with a priority and a label, and "Create another".
        await OpenCreateAsync(amina);
        await ChooseTypeAsync(amina, "Story");
        await Form(amina).GetByLabel("Summary").FillAsync("Refund to card");
        await ChooseParentAsync(amina, "CAW-1");
        await Form(amina).GetByLabel("Assignee").SelectOptionAsync(new SelectOptionValue { Label = "Omar Builder" });
        await Form(amina).GetByLabel("Priority").SelectOptionAsync("High");
        await Form(amina).Locator("#create-labels").FillAsync("payments");
        await Form(amina).Locator("#create-labels").PressAsync("Enter");
        await Assertions.Expect(Form(amina).GetByRole(AriaRole.Button, new() { Name = "Remove label payments" })).ToBeVisibleAsync();
        await Form(amina).GetByTestId("create-another").CheckAsync();
        await amina.SetViewportSizeAsync(360, 740);
        await amina.AssertNoAccessibilityViolationsAsync();
        await amina.SetViewportSizeAsync(1280, 900);
        await Form(amina).GetByTestId("create-submit").ClickAsync();
        await Assertions.Expect(amina.GetByTestId("created-toast")).ToContainTextAsync("CAW-2 created");
        await Assertions.Expect(Form(amina).GetByLabel("Summary")).ToHaveValueAsync("");
        await Assertions.Expect(Form(amina).GetByLabel("Work type")).ToHaveValueAsync("Story");

        // JP_US1_AS3: a sub-task needs its parent; then the story's sub-task.
        await ChooseTypeAsync(amina, "Sub-task");
        await Form(amina).GetByLabel("Summary").FillAsync("Call the bank");
        await Form(amina).GetByTestId("create-submit").ClickAsync();
        await Assertions.Expect(Form(amina).Locator("#create-parent-error")).ToHaveTextAsync("Choose the item this sub-task belongs to.");
        await ChooseParentAsync(amina, "CAW-2");
        await Form(amina).GetByTestId("create-another").UncheckAsync();
        await Form(amina).GetByTestId("create-submit").ClickAsync();
        await Assertions.Expect(amina.GetByTestId("created-toast")).ToContainTextAsync("CAW-3 created");
        await Assertions.Expect(Form(amina).GetByLabel("Summary")).ToHaveCountAsync(0);

        // Each item is where it should be.
        await amina.GetByTestId("created-toast").GetByRole(AriaRole.Link, new() { Name = "View CAW-3" }).ClickAsync();
        await WaitForPathAsync(amina, "/spaces/CAW/board?task=CAW-3");
        await Assertions.Expect(amina.GetByTestId("drawer")).ToContainTextAsync("Sub-task of CAW-2");
        await amina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(amina.GetByTestId("card-CAW-2")).ToContainTextAsync("payments");
        await GotoAsync(amina, "/spaces/CAW/list");
        await Assertions.Expect(amina.GetByTestId("list-row")).ToHaveCountAsync(3);
        await GotoAsync(amina, "/spaces/CAW/board?task=CAW-2");
        await Assertions.Expect(amina.GetByTestId("drawer")).ToContainTextAsync("Omar Builder");
        await Assertions.Expect(amina.GetByTestId("drawer")).ToContainTextAsync("CAW-1 Card refunds");
        await amina.Keyboard.PressAsync("Escape");
        await WaitForPathAsync(amina, "/spaces/CAW/board");

        // JP_US1_AS9: closing with typed text asks first.
        await OpenCreateAsync(amina);
        await Form(amina).GetByLabel("Summary").FillAsync("Book the venue");
        await amina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Form(amina).GetByTestId("discard-confirm")).ToBeVisibleAsync();
        await Form(amina).GetByTestId("keep-editing").ClickAsync();
        await Assertions.Expect(Form(amina).GetByLabel("Summary")).ToHaveValueAsync("Book the venue");
        await Form(amina).GetByRole(AriaRole.Button, new() { Name = "Cancel" }).Last.ClickAsync();
        await Form(amina).GetByTestId("discard").ClickAsync();
        await Assertions.Expect(Form(amina).GetByLabel("Summary")).ToHaveCountAsync(0);

        // JP_US1_AS8: a Viewer everywhere is not offered Create.
        var vera = await SignInAsync("vera.jp1", veraPassword);
        await GotoAsync(vera, "/spaces/CAW/board");
        await Assertions.Expect(vera.GetByTestId("create-button")).ToHaveCountAsync(0);
    }
}
