using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Jira parity pass User Story 3 end to end: the gallery's previews, a blank space with its own columns and work
/// types made with the keyboard, the Create form following its types, a used type that stays on, and a non-member who
/// cannot find the space; with axe scans of the gallery and the Blank setup at both widths.</summary>
public sealed class JP_US3_CreateSpaceTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task JP_US3_Independent_test_a_blank_space_with_its_own_columns_and_work_types()
    {
        var saraPassword = await App.CreateUserAsync("sara.jp3", "Sara Builder");
        var bilalPassword = await App.CreateUserAsync("bilal.jp3", "Bilal Outsider");
        var sara = await SignInAsync("sara.jp3", saraPassword);

        // JP_US3_AS1 and AS2: the gallery, and each template's preview.
        await GotoAsync(sara, "/spaces");
        await sara.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var dialog = sara.GetByTestId("create-project");
        await Assertions.Expect(dialog.GetByRole(AriaRole.Radio)).ToHaveCountAsync(4);
        var preview = dialog.GetByTestId("template-preview");
        await dialog.GetByLabel("Scrum", new() { Exact = true }).CheckAsync();
        await Assertions.Expect(preview).ToContainTextAsync("In Review");
        await Assertions.Expect(preview).ToContainTextAsync("Backlog");
        await dialog.GetByLabel("Stage-gate", new() { Exact = true }).CheckAsync();
        await Assertions.Expect(preview).ToContainTextAsync("Milestone");
        await dialog.GetByLabel("Blank", new() { Exact = true }).CheckAsync();
        await Assertions.Expect(preview).ToContainTextAsync("The work types you choose");
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.SetViewportSizeAsync(360, 740);
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.SetViewportSizeAsync(1280, 900);

        // JP_US3_AS3 and AS4: Blank, with "Building" and "Validating", and Epic, Task and Bug.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await Assertions.Expect(dialog.GetByLabel("Name", new() { Exact = true })).ToBeFocusedAsync();
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Data Platform");
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("DATA");
        await dialog.GetByLabel("Name of column 2").FillAsync("Building");
        await dialog.GetByTestId("blank-add-column").ClickAsync();
        await dialog.GetByLabel("Name of column 3").FillAsync("Validating");
        await dialog.GetByLabel("Story").UncheckAsync();
        await sara.AssertNoAccessibilityViolationsAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/DATA/board");
        await WaitForInteractivityAsync(sara);
        await Assertions.Expect(sara.Locator("[data-testid=column] .col-head h2")).ToHaveTextAsync(["To do", "Building", "Validating", "Done"]);

        // JP_US3_AS5: the Create form offers only the chosen types, and Sub-task.
        await sara.GetByTestId("create-button").ClickAsync();
        var form = sara.GetByTestId("create-work");
        await Assertions.Expect(form.GetByLabel("Work type").Locator("option")).ToHaveTextAsync(["Epic", "Task", "Bug", "Sub-task"]);
        await form.GetByLabel("Work type").SelectOptionAsync(new SelectOptionValue { Label = "Bug" });
        await form.GetByLabel("Summary").FillAsync("Broken export");
        await form.GetByTestId("create-submit").ClickAsync();
        await Assertions.Expect(sara.GetByTestId("created-toast")).ToContainTextAsync("DATA-1 created");

        // JP_US3_AS6: in Space settings, Bug cannot be turned off while an item uses it; Epic can.
        await GotoAsync(sara, "/spaces/DATA/settings");
        var types = sara.GetByTestId("work-types");
        await Assertions.Expect(types).ToContainTextAsync("Bug (1 item)");
        await types.GetByLabel("Bug").UncheckAsync();
        await types.GetByRole(AriaRole.Button, new() { Name = "Save work types" }).ClickAsync();
        await Assertions.Expect(sara.Locator("#work-types-error")).ToHaveTextAsync("Bugs are still used by 1 item in this space.");
        await types.GetByLabel("Bug").CheckAsync();
        await types.GetByLabel("Epic").UncheckAsync();
        await types.GetByRole(AriaRole.Button, new() { Name = "Save work types" }).ClickAsync();
        await Assertions.Expect(types.GetByRole(AriaRole.Status)).ToHaveTextAsync("Saved");

        // JP_US3_AS7: someone who is not a member cannot find it.
        var bilal = await SignInAsync("bilal.jp3", bilalPassword);
        await GotoAsync(bilal, "/spaces/DATA/board");
        await Assertions.Expect(bilal.GetByRole(AriaRole.Heading, new() { Name = "Not found" })).ToBeVisibleAsync();
        await App.AddMemberAsync("DATA", "bilal.jp3", ProjectRole.Viewer);
        await GotoAsync(bilal, "/spaces/DATA/board");
        await Assertions.Expect(bilal.Locator("[data-testid=column]")).ToHaveCountAsync(4);
    }
}
