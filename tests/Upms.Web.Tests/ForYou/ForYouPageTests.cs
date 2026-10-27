using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Pages.ForYou;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.ForYou;

/// <summary>"For you", with Jira's tabs (Jira parity pass US4 scenario 8, FR-022a).</summary>
public sealed class ForYouPageTests : BunitTestBase
{
    private readonly FakeSearchService _search = new();

    public ForYouPageTests() => Services.AddSingleton<ISearchService>(_search);

    private static List<(string? Href, string Text, string? Current)> Tabs<T>(IRenderedComponent<T> cut)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("nav[aria-label='For you'] a").Select(a => (a.GetAttribute("href"), a.TextContent.Trim(), a.GetAttribute("aria-current"))).ToList();

    [Fact]
    public void JP_US4_AS8_Worked_on_is_the_first_tab_and_lists_items_with_their_space()
    {
        _search.WorkedOn.AddRange([FakeSearchService.Row("WEB-7", "Refund to card"), FakeSearchService.Row("MOB-2", "Sign-up crash", "MOB")]);

        var cut = Render<WorkedOnPage>();

        Assert.Equal("For you", cut.Find("h1").TextContent);
        Assert.Equal(
            [("for-you", "Worked on", "page"), ("for-you/viewed", "Viewed", null), ("for-you/assigned", "Assigned to me", null), ("for-you/starred", "Starred", null)],
            Tabs(cut));
        Assert.Equal(["WEB-7", "MOB-2"], cut.FindAll("[data-testid=item-row] .key").Select(k => k.TextContent));
        Assert.Equal("spaces/MOB/board?task=MOB-2", cut.FindAll("[data-testid=item-row] a")[1].GetAttribute("href"));
    }

    [Fact]
    public void Nothing_worked_on_says_so()
    {
        Assert.Contains("Nothing worked on lately", Render<WorkedOnPage>().Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Viewed_lists_the_items_opened_last()
    {
        _search.Recent.Add(FakeSearchService.Row("WEB-4", "Book the venue"));

        var cut = Render<ViewedPage>();

        Assert.Equal("page", Tabs(cut).Single(t => t.Text == "Viewed").Current);
        Assert.Equal(["WEB-4"], cut.FindAll("[data-testid=item-row] .key").Select(k => k.TextContent));
    }

    [Fact]
    public void JP_US4_AS6_Starred_lists_the_starred_spaces_with_their_stars()
    {
        SpaceNavigation.Starred.Add("MOB");

        var cut = Render<StarredPage>();

        Assert.Equal(["Mobile App"], cut.FindAll("[data-testid=starred-spaces] li a").Select(a => a.TextContent));
        cut.Find("[data-testid=star-MOB]").Click();
        Assert.Empty(SpaceNavigation.Starred);
    }
}
