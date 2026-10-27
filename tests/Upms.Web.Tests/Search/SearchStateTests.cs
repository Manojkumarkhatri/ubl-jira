using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Search;

namespace Upms.Web.Tests.Search;

/// <summary>The search page's address (Phase 4 contracts/ui-routes.md): every filter and the sort round-trip, and a saved
/// filter keeps one canonical query string.</summary>
public sealed class SearchStateTests
{
    private static readonly Guid Bilal = Guid.Parse("0000000b-0000-0000-0000-000000000000");

    [Fact]
    public void P4_FR035_Every_filter_round_trips_through_the_address()
    {
        var query = $"q=refund%20card&project=web,MOB&kind=bug&category=todo,done&priority=high&assignee=me,none,{Bilal}"
            + "&creator=me&label=launch,Payments&created=2026-10-01..2026-10-31&updated=..2026-10-15&due=2026-11-01..&sort=due&desc=true&page=3&filter=12";

        var state = SearchState.Parse(query);

        Assert.Equal("refund card", state.Words);
        Assert.Equal(["WEB", "MOB"], state.Projects);
        Assert.Equal([WorkItemType.Bug], state.Kinds);
        Assert.Equal([StatusCategory.ToDo, StatusCategory.Done], state.Categories);
        Assert.Equal([Priority.High], state.Priorities);
        Assert.Equal((true, true, Bilal, true), (state.Me, state.Unassigned, state.People.Single(), state.CreatedByMe));
        Assert.Equal(["launch", "Payments"], state.Labels);
        Assert.Equal(new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), state.Created);
        Assert.Equal(new DateRange(null, new DateOnly(2026, 10, 15)), state.Updated);
        Assert.Equal(new DateRange(new DateOnly(2026, 11, 1), null), state.Due);
        Assert.Equal((SearchSort.DueDate, true, 3, (long?)12), (state.Sort, state.Descending, state.Page, state.FilterId));
        Assert.Equal(state.ToParameters(), SearchState.Parse(string.Join('&', state.ToParameters().Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}"))).ToParameters());
    }

    [Fact]
    public void Defaults_and_nonsense_are_left_out()
    {
        var state = SearchState.Parse("sort=sideways&category=maybe&kind=story,nothing&page=-3&created=yesterday&filter=x");

        Assert.Equal((SearchSort.Updated, true, 1, (long?)null, (DateRange?)null), (state.Sort, state.Descending, state.Page, state.FilterId, state.Created));
        Assert.Empty(state.Categories);
        Assert.Equal([WorkItemType.Story], state.Kinds);
        Assert.Equal("kind=story", state.ToQueryString());
    }

    [Fact]
    public void P4_US3_AS8_A_saved_filter_keeps_the_criteria_but_not_the_page_or_filter()
    {
        var state = SearchState.Parse("q=refund&assignee=none&category=todo&page=2&filter=3");

        Assert.Equal("q=refund&category=todo&assignee=none", state.ToQueryString());
    }

    [Fact]
    public void Me_becomes_the_viewer_in_the_query()
    {
        var me = Guid.NewGuid();

        var query = SearchState.Parse("assignee=me&creator=me").ToQuery(me);

        Assert.True(query.AssignedToMe);
        Assert.Equal([me], query.Creators);
    }
}
