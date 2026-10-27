using Microsoft.Playwright;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Jira parity pass User Story 2 end to end: a Space admin adds a column with "+", renames it, limits it, moves it
/// and deletes it while it holds cards, all from the board and with the keyboard; a Member sees no column controls; with axe
/// scans of the open column menu at both widths.</summary>
public sealed class JP_US2_ColumnsOnBoardTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator ColumnNames(IPage page) => page.Locator("[data-testid=column] .col-head h2");

    private static ILocator Menu(IPage page, string column) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"Column actions for {column}", Exact = true });

    private static async Task ChooseAsync(IPage page, string column, string action)
    {
        await Menu(page, column).ClickAsync();
        await page.GetByRole(AriaRole.Menu).GetByRole(AriaRole.Menuitem, new() { Name = action, Exact = true }).ClickAsync();
    }

    [Fact]
    public async Task JP_US2_Independent_test_add_rename_limit_move_and_delete_a_column_on_the_board()
    {
        var saraPassword = await App.CreateUserAsync("sara.jp2", "Sara Admin");
        var amina = await App.CreateUserAsync("amina.jp2", "Amina Member");
        var sara = await SignInAsync("sara.jp2", saraPassword);
        await GotoAsync(sara, "/spaces");
        await sara.GetByRole(AriaRole.Button, new() { Name = "Create space" }).First.ClickAsync();
        var create = sara.GetByTestId("create-project");
        await create.GetByRole(AriaRole.Button, new() { Name = "Use template" }).ClickAsync();
        await create.GetByLabel("Name").FillAsync("Column Board");
        await Assertions.Expect(create.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await create.GetByLabel("Key").FillAsync("CLB");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await WaitForPathAsync(sara, "/spaces/CLB/board");
        await App.AddMemberAsync("CLB", "amina.jp2", ProjectRole.Member);
        await App.AddTasksAsync("CLB", [("Plan the launch", null, null), ("Write the copy", null, null)]);
        await GotoAsync(sara, "/spaces/CLB/board");
        await Assertions.Expect(ColumnNames(sara)).ToHaveTextAsync(["To Do", "In Progress", "Done"]);

        // JP_US2_AS1: "+" adds "Review", an "in progress" column, at the end.
        await sara.GetByTestId("add-column-button").ClickAsync();
        var add = sara.GetByTestId("board-add-column");
        await add.GetByLabel("Name").FillAsync("Review");
        await add.GetByLabel("Status type").SelectOptionAsync("InProgress");
        await add.GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();
        await Assertions.Expect(ColumnNames(sara)).ToHaveTextAsync(["To Do", "In Progress", "Done", "Review"]);

        // JP_US2_AS2: rename, limit and move it left, with the keyboard.
        await Menu(sara, "Review").FocusAsync();
        await sara.Keyboard.PressAsync("Enter");
        await Assertions.Expect(sara.GetByRole(AriaRole.Menuitem, new() { Name = "Rename" })).ToBeFocusedAsync();
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.Keyboard.PressAsync("Enter");
        var rename = sara.GetByTestId("board-rename-column");
        await rename.GetByLabel("Name").FillAsync("Code review");
        await rename.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(ColumnNames(sara)).ToHaveTextAsync(["To Do", "In Progress", "Done", "Code review"]);
        await ChooseAsync(sara, "Code review", "Set limit");
        await sara.GetByTestId("board-column-limit").GetByLabel("Most cards in the column").FillAsync("3");
        await sara.GetByTestId("board-column-limit").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(sara.GetByTestId("column").Nth(3).Locator(".count")).ToHaveTextAsync("0 of 3");
        await ChooseAsync(sara, "Code review", "Move left");
        await Assertions.Expect(ColumnNames(sara)).ToHaveTextAsync(["To Do", "In Progress", "Code review", "Done"]);
        await Assertions.Expect(Menu(sara, "Code review")).ToBeFocusedAsync();

        // JP_US2_AS3: with cards in it, its status type cannot change.
        foreach (var key in new[] { "CLB-1", "CLB-2" })
        {
            await sara.GetByTestId($"card-{key}").GetByRole(AriaRole.Button, new() { Name = $"Actions for {key}" }).ClickAsync();
            await sara.GetByRole(AriaRole.Group, new() { Name = $"Actions for {key}" })
                .GetByRole(AriaRole.Button, new() { Name = "Code review: bottom" }).ClickAsync();
        }

        await Assertions.Expect(sara.GetByTestId("column").Nth(2).Locator("article.bcard")).ToHaveCountAsync(2);
        await Menu(sara, "Code review").ClickAsync();
        await Assertions.Expect(sara.GetByRole(AriaRole.Menuitem, new() { Name = "Change status type" })).ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(sara.GetByRole(AriaRole.Menu)).ToContainTextAsync("Only empty columns can change type.");
        await sara.Keyboard.PressAsync("Escape");
        await sara.SetViewportSizeAsync(360, 740);
        await Menu(sara, "Code review").ClickAsync();
        await sara.AssertNoAccessibilityViolationsAsync();
        await sara.Keyboard.PressAsync("Escape");
        await sara.SetViewportSizeAsync(1280, 900);

        // JP_US2_AS4: deleting it moves its cards to "In Progress".
        await ChooseAsync(sara, "Code review", "Delete");
        var delete = sara.GetByTestId("delete-column");
        await delete.GetByLabel("Move its work items (2) to").SelectOptionAsync(new SelectOptionValue { Label = "In Progress" });
        await delete.GetByRole(AriaRole.Button, new() { Name = "Delete column" }).ClickAsync();
        await Assertions.Expect(ColumnNames(sara)).ToHaveTextAsync(["To Do", "In Progress", "Done"]);
        await Assertions.Expect(sara.GetByTestId("column").Nth(1).Locator("article.bcard")).ToHaveCountAsync(2);

        // JP_US2_AS5: a Member sees no column controls.
        var member = await SignInAsync("amina.jp2", amina);
        await GotoAsync(member, "/spaces/CLB/board");
        await Assertions.Expect(member.GetByTestId("column")).ToHaveCountAsync(3);
        await Assertions.Expect(member.GetByTestId("add-column-button")).ToHaveCountAsync(0);
        await Assertions.Expect(member.Locator("[data-testid^=column-menu-]")).ToHaveCountAsync(0);
    }
}
