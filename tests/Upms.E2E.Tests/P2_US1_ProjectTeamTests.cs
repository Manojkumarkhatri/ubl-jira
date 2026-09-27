using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Phase 2 User Story 1 end to end: a Project Admin builds the team, and only members see the project.</summary>
public sealed class P2_US1_ProjectTeamTests(AppFixture app) : BrowserTest(app)
{
    private static ILocator Column(IPage page, int index) => page.GetByTestId("column").Nth(index);

    private static ILocator MemberRow(IPage page, string displayName) =>
        page.GetByTestId("member-row").Filter(new() { HasText = displayName });

    private static async Task AddMemberAsync(IPage page, string search, string displayName, string role)
    {
        await page.Locator("#member-search").FillAsync(search);
        await page.GetByRole(AriaRole.Radio, new() { Name = displayName }).CheckAsync();
        await page.Locator("#member-role").SelectOptionAsync(new SelectOptionValue { Label = role });
        await page.Locator("#add-member").ClickAsync();
        await Assertions.Expect(page.GetByTestId("members-saved")).ToContainTextAsync($"{displayName} added as {role}.");
    }

    private static async Task AddTaskAsync(IPage page, string title)
    {
        var box = Column(page, 0).GetByPlaceholder("What needs to be done?");
        await box.FillAsync(title);
        await box.PressAsync("Enter");
    }

    [Fact]
    public async Task P2_US1_Independent_test_a_Project_Admin_builds_the_team_and_only_members_see_the_project()
    {
        var pitaPassword = await App.CreateUserAsync("pita", "Pita Owens");
        var quinnPassword = await App.CreateUserAsync("quinn", "Quinn Rahman");
        var rheaPassword = await App.CreateUserAsync("rhea", "Rhea Kapoor");
        var saulPassword = await App.CreateUserAsync("saul", "Saul Mirza");

        var pita = await SignInAsync("pita", pitaPassword);
        await GotoAsync(pita, "/projects");
        await pita.GetByRole(AriaRole.Button, new() { Name = "Create project" }).First.ClickAsync();
        var dialog = pita.GetByTestId("create-project");
        await dialog.GetByLabel("Name").FillAsync("Team Charter");
        // The suggested key appears first; typing over it before it arrives would mix the two.
        await Assertions.Expect(dialog.GetByLabel("Key")).Not.ToHaveValueAsync("");
        await dialog.GetByLabel("Key").FillAsync("TCH");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();
        await pita.WaitForURLAsync("**/projects/TCH/board");
        await WaitForInteractivityAsync(pita);
        await AddTaskAsync(pita, "Draft the charter");
        await Assertions.Expect(pita.GetByTestId("card-TCH-1")).ToBeVisibleAsync();

        // P2_US1_AS1: the creator is the only member, as Project Admin.
        await pita.GetByRole(AriaRole.Link, new() { Name = "Members" }).ClickAsync();
        await pita.WaitForURLAsync("**/projects/TCH/members");
        await WaitForInteractivityAsync(pita);
        await Assertions.Expect(pita.GetByTestId("member-row")).ToHaveCountAsync(1);
        await Assertions.Expect(MemberRow(pita, "Pita Owens")).ToContainTextAsync("(you)");
        await Assertions.Expect(MemberRow(pita, "Pita Owens").GetByLabel("Role of Pita Owens")).ToHaveValueAsync("ProjectAdmin");

        // P2_US1_AS2: a Member and a Viewer join.
        await AddMemberAsync(pita, "quinn", "Quinn Rahman", "Member");
        await AddMemberAsync(pita, "rhea", "Rhea Kapoor", "Viewer");
        await Assertions.Expect(pita.GetByTestId("member-row")).ToHaveCountAsync(3);
        await pita.AssertNoAccessibilityViolationsAsync();

        // P2_US1_AS3: the Viewer reads the board and a task but is offered no changes.
        var rhea = await SignInAsync("rhea", rheaPassword);
        await GotoAsync(rhea, "/projects/TCH/board");
        await Assertions.Expect(rhea.GetByTestId("card-TCH-1")).ToBeVisibleAsync();
        await Assertions.Expect(rhea.GetByPlaceholder("What needs to be done?")).ToHaveCountAsync(0);
        await Assertions.Expect(rhea.Locator(".move-btn")).ToHaveCountAsync(0);
        await GotoAsync(rhea, "/projects/TCH/board?task=TCH-1");
        await Assertions.Expect(rhea.GetByTestId("drawer-title")).ToHaveTextAsync("Draft the charter");
        await Assertions.Expect(rhea.Locator("#new-comment")).ToHaveCountAsync(0);
        await Assertions.Expect(rhea.Locator("#drawer-status")).ToBeDisabledAsync();

        // P2_US1_AS4: someone outside the team does not see the project at all.
        var saul = await SignInAsync("saul", saulPassword);
        await GotoAsync(saul, "/projects");
        await Assertions.Expect(saul.GetByRole(AriaRole.Link, new() { Name = "Team Charter" })).ToHaveCountAsync(0);
        await saul.GotoAsync("/projects/TCH/board?task=TCH-1");
        await Assertions.Expect(saul.GetByRole(AriaRole.Heading, new() { Name = "Not found" })).ToBeVisibleAsync();

        // P2_US1_AS5: a member removed while her board is open is refused at her next action.
        var quinn = await SignInAsync("quinn", quinnPassword);
        await GotoAsync(quinn, "/projects/TCH/board");
        await AddTaskAsync(quinn, "Collect the goals");
        await Assertions.Expect(quinn.GetByTestId("card-TCH-2")).ToBeVisibleAsync();
        await MemberRow(pita, "Quinn Rahman").GetByRole(AriaRole.Button, new() { Name = "Remove Quinn Rahman" }).ClickAsync();
        await pita.Locator("#confirm-remove").ClickAsync();
        await Assertions.Expect(pita.GetByTestId("members-saved")).ToContainTextAsync("Quinn Rahman removed from the project.");
        await AddTaskAsync(quinn, "One more idea");
        await Assertions.Expect(quinn.GetByRole(AriaRole.Heading, new() { Name = "Not found" })).ToBeVisibleAsync();

        // P2_US1_AS6: the Viewer made a Member can work without signing in again.
        await MemberRow(pita, "Rhea Kapoor").GetByLabel("Role of Rhea Kapoor").SelectOptionAsync(new SelectOptionValue { Label = "Member" });
        await Assertions.Expect(pita.GetByTestId("members-saved")).ToContainTextAsync("Rhea Kapoor is now a Member.");
        await GotoAsync(rhea, "/projects/TCH/board");
        await AddTaskAsync(rhea, "Review the draft");
        await Assertions.Expect(rhea.Locator(".card-title", new() { HasText = "Review the draft" })).ToBeVisibleAsync();

        // P2_US1_AS7: the only Project Admin cannot remove herself.
        await MemberRow(pita, "Pita Owens").GetByRole(AriaRole.Button, new() { Name = "Remove Pita Owens" }).ClickAsync();
        await pita.Locator("#confirm-remove").ClickAsync();
        await Assertions.Expect(pita.GetByRole(AriaRole.Alert)).ToContainTextAsync("last active Project Admin");

        // P2_US1_AS8: an administrator who is not a member has full rights.
        var admin = await SignInAsync(AppFixture.AdminUserName, AppFixture.AdminPassword);
        await GotoAsync(admin, "/projects");
        await Assertions.Expect(admin.GetByRole(AriaRole.Row).Filter(new() { HasText = "Team Charter" })).ToContainTextAsync("Administrator access");
        await GotoAsync(admin, "/projects/TCH/members");
        await Assertions.Expect(admin.Locator("#member-search")).ToBeVisibleAsync();
        await admin.SetViewportSizeAsync(360, 740);
        await admin.AssertNoAccessibilityViolationsAsync();
    }
}
