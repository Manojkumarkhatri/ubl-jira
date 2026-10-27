using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Layout;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Layout;

/// <summary>The search box on every screen (Phase 4 FR-032, FR-033; research R16, R25): a combobox with recent items, key
/// jumps and word searches.</summary>
public sealed class SearchBoxTests : BunitTestBase
{
    private readonly FakeSearchService _search = new();

    public SearchBoxTests()
    {
        Services.AddSingleton<ISearchService>(_search);
    }

    private string Uri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public void P4_FR033_The_box_is_a_labelled_combobox()
    {
        var input = Render<SearchBox>().Find("#global-search");

        Assert.Equal(("combobox", "Search or type a key", "false", "global-search-list"),
            (input.GetAttribute("role"), input.GetAttribute("aria-label"), input.GetAttribute("aria-expanded"), input.GetAttribute("aria-controls")));
    }

    [Fact]
    public void P4_US3_AS3_Focused_while_empty_it_lists_recent_items_to_open_by_keyboard()
    {
        _search.Recent.AddRange([FakeSearchService.Row("WEB-4", "Book the venue"), FakeSearchService.Row("MOB-12", "Refund to card", "MOB")]);
        var cut = Render<SearchBox>();

        cut.Find("#global-search").Focus();

        Assert.Equal("true", cut.Find("#global-search").GetAttribute("aria-expanded"));
        Assert.Equal(["WEB-4", "MOB-12"], cut.FindAll("[role=option] .key").Select(k => k.TextContent));
        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("global-search-option-1", cut.Find("#global-search").GetAttribute("aria-activedescendant"));
        Assert.Equal("true", cut.Find("#global-search-option-1").GetAttribute("aria-selected"));

        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.EndsWith("spaces/MOB/board?task=MOB-12", Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void P4_US3_AS1_A_key_and_Enter_open_the_item()
    {
        _search.Keys.Add("MOB-12");
        var cut = Render<SearchBox>();

        cut.Find("#global-search").Input("mob-12");
        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.EndsWith("spaces/MOB/board?task=MOB-12", Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void P4_US3_AS2_A_key_that_cannot_be_opened_says_so()
    {
        var cut = Render<SearchBox>();

        cut.Find("#global-search").Input("MOB-9999");
        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal("No work item MOB-9999 was found.", cut.Find("#global-search-message").TextContent));
        Assert.Equal("http://localhost/", Uri);
    }

    [Fact]
    public void P4_US3_AS4_Words_and_Enter_open_the_search_page()
    {
        var cut = Render<SearchBox>();

        cut.Find("#global-search").Input("refund card");
        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.EndsWith("search?q=refund%20card", Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_closes_the_list()
    {
        _search.Recent.Add(FakeSearchService.Row("WEB-4", "Book the venue"));
        var cut = Render<SearchBox>();
        cut.Find("#global-search").Focus();

        cut.Find("#global-search").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }
}
