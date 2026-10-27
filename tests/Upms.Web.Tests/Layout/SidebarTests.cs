using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Layout;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Layout;

/// <summary>The sidebar on every page, as in today's Jira Cloud (Jira parity pass US4 scenarios 3, 4 and 6; FR-021, FR-021b).</summary>
public sealed class SidebarTests : BunitTestBase
{
    private readonly FakeSearchService _search = new();
    private readonly FakeAccountService _accounts = new();

    public SidebarTests()
    {
        Services.AddSingleton<ISearchService>(_search);
        Services.AddSingleton<ISavedFilterService>(new FakeSavedFilterService(CurrentUser.UserId!.Value));
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(_accounts);
        Services.AddSingleton<Upms.Application.Notifications.INotificationService>(new FakeNotificationService());
    }

    private static List<string> Sections(IRenderedComponent<Sidebar> cut) =>
        cut.FindAll(".side-list > li > a, .side-list > li > button").Select(e => e.TextContent.Trim()).Where(t => t.Length > 0).ToList();

    [Fact]
    public void JP_US4_AS3_For_you_recent_starred_spaces_and_filters_and_the_links_each_type_may_use()
    {
        // A Member: their spaces' MIS, and no "Create space" (owner's decision of 2026-10-20).
        Organization.Access = new(Upms.Domain.Common.UserType.Member, null, Upms.Domain.Common.RightsCatalog.Defaults(Upms.Domain.Common.UserType.Member), false);
        var cut = Render<Sidebar>();

        Assert.Equal("Main", cut.Find("nav").GetAttribute("aria-label"));
        Assert.Equal(["For you", "Recent", "Starred", "Spaces", "Filters", "MIS reports"], Sections(cut));
        Assert.Empty(cut.FindAll("a.side-add"));
    }

    [Fact]
    public void An_Admin_also_gets_portfolios_people_and_the_ticks()
    {
        var cut = Render<Sidebar>();

        Assert.Equal(["For you", "Recent", "Starred", "Spaces", "Filters", "MIS reports", "Portfolios", "People", "Types and ticks"], Sections(cut));
        Assert.Equal("Create space", cut.Find("a.side-add").GetAttribute("aria-label"));
    }

    [Fact]
    public void Spaces_is_open_and_the_other_sections_open_on_demand()
    {
        SpaceNavigation.Visits.AddRange(["WEB", "MOB"]);
        _search.Recent.Add(FakeSearchService.Row("WEB-4", "Book the venue"));
        var cut = Render<Sidebar>();

        var recent = cut.FindAll("button.side-section").Single(b => b.TextContent.Trim() == "Recent");
        var spaces = cut.FindAll("button.side-section").Single(b => b.TextContent.Trim() == "Spaces");
        Assert.Equal(("false", "true"), (recent.GetAttribute("aria-expanded"), spaces.GetAttribute("aria-expanded")));
        Assert.True(cut.Find($"#{recent.GetAttribute("aria-controls")}").HasAttribute("hidden"));
        Assert.Equal(["Mobile App", "Website Revamp"],
            cut.Find($"#{spaces.GetAttribute("aria-controls")}").QuerySelectorAll("a:not(.side-more)").Select(a => a.TextContent.Trim()[2..].Trim()));

        recent.Click();

        var group = cut.Find($"#{cut.FindAll("button.side-section")[0].GetAttribute("aria-controls")}");
        Assert.False(group.HasAttribute("hidden"));
        Assert.Contains("WEB-4", group.TextContent, StringComparison.Ordinal);
        Assert.Equal("spaces/WEB/board?task=WEB-4", group.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void JP_US4_AS6_Starred_spaces_come_first_and_the_current_space_is_marked()
    {
        SpaceNavigation.Starred.Add("CBU");
        SpaceNavigation.Visits.Add("WEB");
        Services.GetRequiredService<NavigationManager>().NavigateTo("spaces/WEB/list");

        var cut = Render<Sidebar>();

        var spaces = cut.FindAll("button.side-section").Single(b => b.TextContent.Trim() == "Spaces");
        var links = cut.Find($"#{spaces.GetAttribute("aria-controls")}").QuerySelectorAll("a:not(.side-more)");
        Assert.Equal(["spaces/CBU", "spaces/WEB"], links.Select(a => a.GetAttribute("href")));
        Assert.Equal("page", links[1].GetAttribute("aria-current"));
    }

    [Fact]
    public void JP_US4_AS4_The_header_button_collapses_the_sidebar_and_keeps_the_choice()
    {
        var cut = Render<Header>();

        cut.Find("[data-testid=sidebar-toggle]").Click();

        Assert.Equal([true], _accounts.SidebarChoices);
        Assert.Equal("false", cut.Find("[data-testid=sidebar-toggle]").GetAttribute("aria-expanded"));
        Assert.True(Services.GetRequiredService<SidebarState>().Collapsed);
        Assert.Equal("dialog", cut.Find("[data-testid=menu-button]").GetAttribute("aria-haspopup"));
    }

    [Fact]
    public async Task The_saved_choice_applies_from_the_first_page()
    {
        _accounts.SidebarCollapsed = true;
        var state = Services.GetRequiredService<SidebarState>();

        await state.EnsureChoiceAsync();

        Assert.True(state.Collapsed);
    }
}
