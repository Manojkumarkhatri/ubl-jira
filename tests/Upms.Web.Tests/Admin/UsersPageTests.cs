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

/// <summary>People and their access (FR-008; owner's decision of 2026-10-20): each person's type, department and ticks.</summary>
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
        cut.FindAll("tbody tr").Single(r => r.QuerySelectorAll(".person-cell .muted").Any(m => m.TextContent == userName));

    private static IElement? Button(IElement row, string text) =>
        row.QuerySelectorAll("button").SingleOrDefault(b => b.TextContent.Trim() == text);

    private static IElement Tick(IRenderedComponent<Users> cut, string name) =>
        cut.FindAll(".tick-row").Single(r => r.QuerySelector("span")!.TextContent == name).QuerySelector("input")!;

    [Fact]
    public void Each_person_shows_their_type_badge_and_department()
    {
        var cut = Render<Users>();

        Assert.Equal("Admin", Row(cut, "ada").QuerySelector(".type-badge")!.TextContent);
        Assert.Equal("Member", Row(cut, "amina").QuerySelector(".type-badge")!.TextContent);
        Assert.Contains("(you)", Row(cut, "ada").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("(you)", Row(cut, "amina").TextContent, StringComparison.Ordinal);
        Assert.NotNull(Button(Row(cut, "dora"), "Reactivate"));
    }

    [Fact]
    public void Making_a_colleague_an_Admin_saves_their_type_and_shows_the_badge()
    {
        var cut = Render<Users>();
        var access = Button(Row(cut, "amina"), "Access")!;
        Assert.Equal("Access: amina", access.GetAttribute("aria-label"));

        access.Click();
        Assert.Equal("Access for amina Tester", cut.Find("#access-heading").TextContent);
        cut.Find("#access-type").Change("Admin");
        cut.Find("#save-access").Click();

        Assert.Equal(["access amina Admin"], _accounts.Calls);
        Assert.Equal("Admin", Row(cut, "amina").QuerySelector(".type-badge")!.TextContent);
        Assert.Empty(cut.FindAll("[data-testid=access-editor]"));
        Assert.Contains("Access for amina Tester saved.", _announced);
    }

    [Fact]
    public void A_new_type_starts_from_its_own_ticks_and_a_changed_tick_is_marked()
    {
        var cut = Render<Users>();
        Button(Row(cut, "amina"), "Access")!.Click();

        Assert.True(Tick(cut, "Create and edit work").IsChecked());
        Assert.False(Tick(cut, "Create spaces").IsChecked());

        cut.Find("#access-type").Change("PortfolioLead");
        Assert.True(Tick(cut, "Create spaces").IsChecked());
        Assert.Empty(cut.FindAll(".tick-changed"));

        Tick(cut, "Create spaces").Change(false);
        Assert.Single(cut.FindAll(".tick-changed"));
    }

    [Fact]
    public void Cancelling_changes_nothing_and_returns_focus_to_the_Access_button()
    {
        var cut = Render<Users>();
        var amina = _accounts.Users.Single(u => u.UserName == "amina");

        Button(Row(cut, "amina"), "Access")!.Click();
        cut.Find("#access-type").Change("Head");
        cut.FindAll("[data-testid=access-editor] button").Single(b => b.TextContent == "Cancel").Click();
        cut.Render();

        Assert.Empty(_accounts.Calls);
        Assert.Equal("Member", Row(cut, "amina").QuerySelector(".type-badge")!.TextContent);
        Assert.Equal($"Access-{amina.Id:N}", JSInterop.Invocations.Last(i => i.Identifier == "upms.focusById").Arguments[0]);
    }

    [Fact]
    public void FR008_The_last_administrator_is_told_why_the_type_stays()
    {
        _accounts.NextError = AppError.Rule(ErrorCodes.LastAdministrator,
            "This is the last active administrator. Make someone else an administrator first.");
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("admin/users");
        var cut = Render<Users>();

        Button(Row(cut, "ada"), "Access")!.Click();
        cut.Find("#access-type").Change("Member");
        cut.Find("#save-access").Click();

        Assert.Equal("This is the last active administrator. Make someone else an administrator first.",
            cut.Find("[data-testid=access-editor] [role=alert]").TextContent);
        Assert.Equal("Admin", Row(cut, "ada").QuerySelector(".type-badge")!.TextContent);
        Assert.EndsWith("/admin/users", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void FR008_Giving_up_your_own_Admin_type_leaves_the_page()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("admin/users");
        var cut = Render<Users>();

        Button(Row(cut, "ada"), "Access")!.Click();
        cut.Find("#access-type").Change("Member");
        cut.Find("#save-access").Click();

        Assert.Equal(["access ada Member"], _accounts.Calls);
        Assert.EndsWith("/for-you", navigation.Uri, StringComparison.Ordinal);
    }
}
