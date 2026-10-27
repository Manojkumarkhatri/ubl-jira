using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Projects;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Search;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Search;

/// <summary>The search page (Phase 4 FR-034–FR-041; contracts/ui-routes.md): results from the address, filters as removable
/// chips, sorting and paging, the drawer over the results, and the saved-filter panel.</summary>
public sealed class SearchPageTests : BunitTestBase
{
    private readonly FakeSearchService _search = new();
    private readonly FakeSavedFilterService _filters;
    private readonly FakeWorkItemService _items = new();

    public SearchPageTests()
    {
        _filters = new FakeSavedFilterService(CurrentUser.UserId!.Value);
        Services.AddSingleton<ISearchService>(_search);
        Services.AddSingleton<ISavedFilterService>(_filters);
        Services.AddSingleton<IProjectService>(new FakeProjectService());
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private string Query => new Uri(Navigation.Uri).Query;

    private IRenderedComponent<SearchPage> Open(string query = "")
    {
        Navigation.NavigateTo($"search{query}");
        return Render<SearchPage>();
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<SearchPage> cut, string text) =>
        cut.FindAll("[data-testid=saved-filters] button").First(b => b.TextContent.Trim() == text);

    [Fact]
    public void P4_US3_AS4_Results_show_with_their_count()
    {
        _search.Rows.AddRange([FakeSearchService.Row("WEB-1", "Refund to card"), FakeSearchService.Row("MOB-2", "Refund history", "MOB")]);

        var cut = Open("?q=refund");

        Assert.Equal("2 results", cut.Find("[data-testid=result-count]").TextContent);
        Assert.Equal(["Refund to card", "Refund history"], cut.FindAll("[data-testid=search-result] a").Select(a => a.TextContent));
        Assert.Equal("refund", _search.Queries.Last().Words);
        Assert.Equal("refund", cut.Find("#search-words").GetAttribute("value"));
    }

    [Fact]
    public void P4_US3_AS5_Active_filters_show_as_chips_that_remove_one_filter()
    {
        var cut = Open("?q=refund&category=todo&assignee=none");

        var chips = cut.FindAll("[data-testid=search-chips] .chip");
        Assert.Equal(2, chips.Count);
        cut.Find("[aria-label='Remove filter Status type: To do']").Click();

        Assert.Equal("?q=refund&assignee=none", Query);
    }

    [Fact]
    public void Choosing_a_filter_puts_it_in_the_address()
    {
        var cut = Open("?q=refund");

        cut.Find("#filter-priority").Change("high");

        Assert.Equal("?q=refund&priority=high", Query);
    }

    [Fact]
    public void A_new_filter_builds_on_the_last_one_asked_for()
    {
        var slow = new SlowNavigationManager("search?q=refund");
        Services.AddSingleton<NavigationManager>(slow);
        var cut = Render<SearchPage>();
        slow.Holding = true;

        cut.Find("#filter-priority").Change("high");
        cut.Find("#filter-assignee").Change("me");
        cut.InvokeAsync(slow.Arrive);

        Assert.Equal("?q=refund&priority=high&assignee=me", new Uri(slow.Uri).Query);
    }

    [Fact]
    public void P4_US3_AS6_Sorting_and_paging_change_the_address()
    {
        for (var i = 0; i < 60; i++)
        {
            _search.Rows.Add(FakeSearchService.Row($"WEB-{i + 1}", $"Item {i}"));
        }

        var cut = Open("?q=item");

        cut.Find("#search-sort").Change("priority");
        Assert.Equal("?q=item&sort=priority", Query);
        cut.Find("[data-testid=sort-direction]").Click();
        Assert.Equal("?q=item&sort=priority&desc=true", Query);
        cut.WaitForAssertion(() => cut.Find("nav.pager button:last-child").Click());
        Assert.Equal("?q=item&sort=priority&desc=true&page=2", Query);
    }

    [Fact]
    public void P4_US3_AS9_A_result_opens_in_the_drawer_and_closing_returns_focus_to_it()
    {
        _search.Rows.Add(FakeSearchService.Row("WEB-1", "Refund to card"));
        var cut = Open("?q=refund");

        Navigation.NavigateTo(cut.Find("#search-result-WEB-1").GetAttribute("href")!);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=drawer]")));
        cut.Find("[aria-label='Close task details']").Click();

        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusByIdIfLost"
            && Equals(i.Arguments[0], "search-result-WEB-1")));
        Assert.Equal("?q=refund", Query);
    }

    [Fact]
    public void P4_FR041_Values_that_no_longer_apply_are_reported()
    {
        _search.Unavailable.Add("Space PAY");

        var cut = Open("?project=PAY");

        Assert.Contains("Space PAY is no longer available to you", cut.Find("[data-testid=unavailable]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P4_US3_AS8_The_current_search_is_saved_as_a_filter()
    {
        var cut = Open("?q=refund&assignee=none");

        cut.Find("#filter-name").Input("Unassigned refunds");
        cut.Find(".filter-save").Submit();

        var saved = Assert.Single(_filters.Filters).View;
        Assert.Equal(("Unassigned refunds", "q=refund&assignee=none"), (saved.Name, saved.Query));
        cut.WaitForAssertion(() => Assert.Equal($"?q=refund&assignee=none&filter={saved.Id}", Query));
    }

    [Fact]
    public void A_taken_name_is_explained()
    {
        _filters.Add("Unassigned refunds", "q=x");
        var cut = Open("?q=refund");

        cut.Find("#filter-name").Input("unassigned refunds");
        cut.Find(".filter-save").Submit();

        Assert.Contains("already have a filter called", cut.Find("#filter-name-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_a_saved_filter_runs_its_search()
    {
        var filter = _filters.Add("Unassigned refunds", "q=refund&assignee=none");

        Open($"?filter={filter.Id}");

        Assert.Equal($"?filter={filter.Id}&q=refund&assignee=none", Query);
    }

    [Fact]
    public void P4_US3_AS8_Own_filters_can_be_renamed_updated_shared_and_deleted()
    {
        var filter = _filters.Add("Refunds", "q=refund");
        var cut = Open($"?q=refund&priority=high&filter={filter.Id}");

        Button(cut, "Update with this search").Click();
        cut.WaitForAssertion(() => Assert.Equal("q=refund&priority=high", _filters.Filters[0].View.Query));

        Button(cut, "Rename").Click();
        cut.Find("#filter-rename").Input("High refunds");
        Button(cut, "Rename").Click();
        cut.WaitForAssertion(() => Assert.Equal("High refunds", _filters.Filters[0].View.Name));

        Button(cut, "Share").Click();
        cut.FindAll(".saved-filters input[type=checkbox]")[0].Change(true);
        Button(cut, "Save sharing").Click();
        cut.WaitForAssertion(() => Assert.Equal(["WEB"], _filters.Filters[0].View.SharedWith.Select(p => p.Key)));

        Button(cut, "Delete").Click();
        cut.Find("[data-testid=confirm-delete-filter]").Click();
        cut.WaitForAssertion(() => Assert.Empty(_filters.Filters));
        Assert.Equal("?q=refund&priority=high", Query);
    }

    [Fact]
    public void P4_US3_AS10_Filters_shared_with_me_show_their_owner_and_offer_no_changes()
    {
        var shared = _filters.Add("Open payment bugs", "q=bug", mine: false, owner: "Sara Malik");

        var cut = Open($"?q=bug&filter={shared.Id}");

        Assert.Contains("by Sara Malik", cut.Find("[data-testid=shared-filters]").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(cut.FindAll("[data-testid=saved-filters] button"), b => b.TextContent.Trim() is "Rename" or "Delete");
    }

    [Fact]
    public void P4_US4_AS6_A_label_filter_is_added_from_the_search_page()
    {
        var cut = Open("?q=refund");

        cut.Find("#filter-label").Input("launch");
        cut.Find("#filter-label").Closest("form")!.Submit();

        Assert.Equal("?q=refund&label=launch", Query);
        Assert.Equal(["launch"], _search.Queries.Last().Labels);
        Assert.Contains(cut.FindAll("[data-testid=search-chips] .chip"), c => c.TextContent.Contains("Label: launch", StringComparison.Ordinal));
    }
}
