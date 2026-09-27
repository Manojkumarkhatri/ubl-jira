using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.List;

namespace Upms.Web.Tests.List;

/// <summary>The List view's state in its address (Phase 2 FR-031).</summary>
public sealed class ListStateTests
{
    private static readonly Guid Bilal = Guid.Parse("7b0c3f6e-2d6a-4f7e-9c1e-5b4a3c2d1e0f");

    [Fact]
    public void P2_US3_AS4_The_address_round_trips_into_the_query_and_back()
    {
        var state = ListState.Parse(sort: "due", dir: "desc", column: "2", type: "inprogress,done", priority: "high",
            assignee: $"me,none,{Bilal}", due: "overdue", q: " login ", page: 2);

        var query = state.ToQuery();
        Assert.Equal((ListSort.DueDate, true, 2), (query.Sort, query.Descending, query.Page));
        Assert.Equal([2L], query.ColumnIds!);
        Assert.Equal([StatusCategory.InProgress, StatusCategory.Done], query.Categories!);
        Assert.Equal([Priority.High], query.Priorities!);
        Assert.Equal((true, true), (query.AssignedToMe, query.Unassigned));
        Assert.Equal([Bilal], query.AssigneeIds!);
        Assert.Equal((DueFilter.Overdue, "login"), (query.Due, query.Text));

        var parameters = state.ToParameters();
        Assert.Equal("due", parameters["sort"]);
        Assert.Equal("desc", parameters["dir"]);
        Assert.Equal("2", parameters["column"]);
        Assert.Equal("inprogress,done", parameters["type"]);
        Assert.Equal("high", parameters["priority"]);
        Assert.Equal($"me,none,{Bilal}", parameters["assignee"]);
        Assert.Equal("overdue", parameters["due"]);
        Assert.Equal("login", parameters["q"]);
        Assert.Equal(2, parameters["page"]);
    }

    [Fact]
    public void The_default_list_leaves_the_address_clean()
    {
        var state = ListState.Parse(null, null, null, null, null, null, null, null, null);

        Assert.Equal((ListSort.Key, true, 1), (state.Sort, state.Descending, state.Page));
        Assert.All(state.ToParameters().Values, Assert.Null);
        Assert.False(state.HasFilters);
    }

    [Fact]
    public void Unknown_values_are_ignored()
    {
        var state = ListState.Parse("colour", "sideways", "x,5", "later", "urgent", "someone", "soon", null, -3);

        Assert.Equal((ListSort.Key, true, 1), (state.Sort, state.Descending, state.Page));
        Assert.Equal([5L], state.Columns);
        Assert.Empty(state.Types);
        Assert.Empty(state.Priorities);
        Assert.Equal((false, false), (state.Me, state.Unassigned));
        Assert.Empty(state.People);
        Assert.Equal(DueFilter.Any, state.Due);
    }

    [Fact]
    public void Choosing_a_column_sorts_by_it_and_choosing_it_again_reverses_the_order()
    {
        var byDue = ListState.Default.WithSort(ListSort.DueDate);
        var reversed = byDue.WithSort(ListSort.DueDate);
        var byUpdate = ListState.Default.WithSort(ListSort.Updated);

        Assert.Equal((ListSort.DueDate, false), (byDue.Sort, byDue.Descending));
        Assert.True(reversed.Descending);
        Assert.True(byUpdate.Descending); // the newest first, like keys
        Assert.Equal(1, (ListState.Default with { Page = 3 }).WithSort(ListSort.Title).Page);
    }

    [Fact]
    public void Changing_a_filter_goes_back_to_the_first_page_and_clearing_keeps_the_sort()
    {
        var state = ListState.Default.WithSort(ListSort.Title) with { Page = 4 };

        var filtered = state.WithDue(DueFilter.Next7Days);
        var cleared = filtered.WithAssignee("me").WithText("draft").WithoutFilters();

        Assert.Equal((DueFilter.Next7Days, 1), (filtered.Due, filtered.Page));
        Assert.False(cleared.HasFilters);
        Assert.Equal((ListSort.Title, false), (cleared.Sort, cleared.Descending));
    }
}
