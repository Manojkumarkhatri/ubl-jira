using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Upms.E2E.Tests.Fixtures;

namespace Upms.E2E.Tests;

/// <summary>Sign-in, forced password change, profile and account administration in a real browser.</summary>
public sealed partial class FoundationJourneyTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task The_sign_in_page_is_accessible()
    {
        var page = await NewPageAsync();
        await page.GotoAsync("/Account/Login");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in" })).ToBeVisibleAsync();
        await page.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task A_temporary_password_is_replaced_at_first_sign_in_then_the_profile_opens()
    {
        var temporary = await App.CreateUserAsync("farah", "Farah Ali", mustChangePassword: true);

        var page = await SignInAsync("farah", temporary);
        await Assertions.Expect(page).ToHaveURLAsync(ChangePasswordUrl());
        await page.GetByLabel("Temporary password").FillAsync(temporary);
        await page.GetByLabel("New password", new() { Exact = true }).FillAsync("a password of my own");
        await page.GetByLabel("Confirm the new password").FillAsync("a password of my own");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save the new password" }).ClickAsync();

        await page.GotoAsync("/account/profile");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = true })).ToBeVisibleAsync();
        await page.GetByLabel("Display name", new() { Exact = true }).FillAsync("Farah A.");
        await page.GetByLabel("Time zone", new() { Exact = true }).SelectOptionAsync("Asia/Karachi");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Saved", new() { Exact = true })).ToBeVisibleAsync();
        await page.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task An_administrator_adds_an_account_and_sees_the_temporary_password_once()
    {
        var page = await SignInAsync(AppFixture.AdminUserName, AppFixture.AdminPassword);
        await page.GotoAsync("/admin/users");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Accounts" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add user" }).ClickAsync();
        await page.GetByLabel("User name").FillAsync("gul");
        await page.GetByLabel("Display name").FillAsync("Gul Khan");
        await page.GetByLabel("Email address").FillAsync("gul@example.com");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();

        var secret = page.GetByTestId("temporary-password");
        await Assertions.Expect(secret).ToContainTextAsync("Account gul created.");
        await Assertions.Expect(secret.Locator("code")).ToHaveTextAsync(TemporaryPassword());
        await page.AssertNoAccessibilityViolationsAsync();
    }

    [Fact]
    public async Task Regular_users_cannot_open_account_administration()
    {
        var password = await App.CreateUserAsync("hina", "Hina Shah");

        var page = await SignInAsync("hina", password);
        await page.GotoAsync("/admin/users");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Not found" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Accounts" })).ToHaveCountAsync(0);
    }

    [GeneratedRegex("/Account/ChangePassword")]
    private static partial Regex ChangePasswordUrl();

    [GeneratedRegex("^[A-Za-z0-9]{16}$")]
    private static partial Regex TemporaryPassword();
}
