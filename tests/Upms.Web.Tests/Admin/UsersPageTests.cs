using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Domain.Identity;
using Upms.Web.Components.Pages.Admin;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Admin;

/// <summary>Giving and removing the Administrator role on the Accounts page (FR-008).</summary>
public sealed class UsersPageTests : BunitTestBase
{
    private readonly FakeUserAdminService _accounts = new();
    private readonly List<string> _announced = [];

    public UsersPageTests()
    {
        Services.AddSingleton<IUserAdminService>(_accounts);
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        _accounts.Add("ada", OrganizationRole.Administrator, id: CurrentUser.UserId);
        _accounts.Add("amina");
        _accounts.Add("dora", isActive: false);
    }

    private static IElement Row(IRenderedComponent<Users> cut, string userName) =>
        cut.FindAll("tbody tr").Single(r => r.QuerySelector("td")!.LastElementChild!.TextContent == userName);

    private static IElement? Button(IElement row, string text) =>
        row.QuerySelectorAll("button").SingleOrDefault(b => b.TextContent.Trim() == text);

    [Fact]
    public void FR008_Making_a_colleague_an_administrator_asks_first_then_shows_the_new_role()
    {
        var cut = Render<Users>();
        var make = Button(Row(cut, "amina"), "Make administrator")!;
        Assert.Equal("Make administrator: amina", make.GetAttribute("aria-label"));

        make.Click();

        Assert.Empty(_accounts.Calls);
        Assert.Contains("Make amina an administrator? They will be able to manage accounts and every project.",
            Row(cut, "amina").TextContent, StringComparison.Ordinal);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusById" && Equals(i.Arguments[0], "confirm-action"));

        Button(Row(cut, "amina"), "Yes, make administrator")!.Click();

        Assert.Equal(["role amina Administrator"], _accounts.Calls);
        var row = Row(cut, "amina");
        Assert.Contains("Administrator", row.QuerySelectorAll("td")[2].TextContent, StringComparison.Ordinal);
        Assert.NotNull(Button(row, "Remove administrator"));
        Assert.Null(Button(row, "Make administrator"));
        Assert.Contains("amina is now an administrator.", _announced);
    }

    [Fact]
    public void Cancelling_changes_nothing_and_returns_focus_to_the_button()
    {
        var cut = Render<Users>();
        var amina = _accounts.Users.Single(u => u.UserName == "amina");

        Button(Row(cut, "amina"), "Make administrator")!.Click();
        Button(Row(cut, "amina"), "Cancel")!.Click();

        Assert.Empty(_accounts.Calls);
        Assert.NotNull(Button(Row(cut, "amina"), "Make administrator"));
        Assert.Equal($"MakeAdministrator-{amina.Id:N}", JSInterop.Invocations.Last(i => i.Identifier == "upms.focusById").Arguments[0]);
    }

    [Fact]
    public void FR008_The_last_administrator_is_told_why_the_role_stays()
    {
        _accounts.NextError = AppError.Rule(ErrorCodes.LastAdministrator,
            "This is the last active administrator. Make someone else an administrator first.");
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("admin/users");
        var cut = Render<Users>();

        Button(Row(cut, "ada"), "Remove administrator")!.Click();
        Assert.Contains("Remove your own administrator rights? You will no longer be able to manage accounts.",
            Row(cut, "ada").TextContent, StringComparison.Ordinal);
        Button(Row(cut, "ada"), "Yes, remove")!.Click();

        Assert.Equal("This is the last active administrator. Make someone else an administrator first.",
            cut.Find("[role=alert]").TextContent);
        Assert.Contains("Administrator", Row(cut, "ada").QuerySelectorAll("td")[2].TextContent, StringComparison.Ordinal);
        Assert.EndsWith("/admin/users", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void FR008_Removing_your_own_role_leaves_the_administrators_page()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("admin/users");
        var cut = Render<Users>();

        Button(Row(cut, "ada"), "Remove administrator")!.Click();
        Button(Row(cut, "ada"), "Yes, remove")!.Click();

        Assert.Equal(["role ada User"], _accounts.Calls);
        Assert.EndsWith("/projects", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("ada is no longer an administrator.", _announced);
    }

    [Fact]
    public void Your_own_row_is_marked_and_deactivated_accounts_are_not_offered_the_role()
    {
        var cut = Render<Users>();

        Assert.Contains("(you)", Row(cut, "ada").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("(you)", Row(cut, "amina").TextContent, StringComparison.Ordinal);
        Assert.Null(Button(Row(cut, "dora"), "Make administrator"));
        Assert.NotNull(Button(Row(cut, "dora"), "Reactivate"));
    }
}
