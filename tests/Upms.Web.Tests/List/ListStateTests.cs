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

    [Fact]
    public void P3_US1_AS8_Types_and_epics_or_stages_round_trip_through_the_address()
    {
        var state = ListState.Parse(sort: "type", dir: null, column: null, type: "todo", priority: null, assignee: null, due: null,
            q: null, page: null, kind: "bug,Story,nonsense", container: "mob-1,none");

        Assert.Equal((ListSort.Type, false), (state.Sort, state.Descending));
        Assert.Equal([StatusCategory.ToDo], state.Types);
        Assert.Equal([WorkItemType.Bug, WorkItemType.Story], state.ItemTypes);
        Assert.Equal(["MOB-1"], state.Containers);
        Assert.True(state.NoContainer);
        Assert.True(state.HasFilters);

        var query = state.ToQuery();
        Assert.Equal([WorkItemType.Bug, WorkItemType.Story], query.Types);
        Assert.Equal(["MOB-1"], query.ContainerKeys);
        Assert.True(query.NoContainer);

        var parameters = state.ToParameters();
        Assert.Equal("type", parameters["sort"]);
        Assert.Equal("todo", parameters["type"]);
        Assert.Equal("bug,story", parameters["kind"]);
        Assert.Equal("MOB-1,none", parameters["container"]);
    }

    [Fact]
    public void Choosing_a_type_or_an_epic_filters_and_goes_back_to_the_first_page()
    {
        var state = ListState.Default with { Page = 3 };

        var bugs = state.WithItemType(WorkItemType.Bug);
        var inEpic = bugs.WithContainer("MOB-1");
        var none = bugs.WithContainer(ListState.NoContainerValue);

        Assert.Equal([WorkItemType.Bug], bugs.ItemTypes);
        Assert.Equal(1, bugs.Page);
        Assert.Equal(["MOB-1"], inEpic.Containers);
        Assert.False(inEpic.NoContainer);
        Assert.Empty(none.Containers);
        Assert.True(none.NoContainer);
        Assert.False(bugs.WithItemType(null).WithContainer("").HasFilters);
        Assert.Null(ListState.Default.ToParameters()["kind"]);
        Assert.Null(ListState.Default.ToParameters()["container"]);
    }

    [Fact]
    public void P4_US4_AS6_Labels_round_trip_as_typed_and_toggle_on_and_off()
    {
        var state = ListState.Parse(null, null, null, null, null, null, null, null, page: 2, labels: "Launch, design,launch");

        Assert.Equal(["Launch", "design"], state.Labels);
        Assert.True(state.HasFilters);
        Assert.Equal(["Launch", "design"], state.ToQuery().Labels);
        Assert.Equal("Launch,design", state.ToParameters()["labels"]);

        var fewer = state.ToggleLabel("LAUNCH");
        Assert.Equal(["design"], fewer.Labels);
        Assert.Equal(1, fewer.Page);
        Assert.Equal(["design", "pricing"], fewer.ToggleLabel("pricing").Labels);
        Assert.Null(ListState.Default.ToParameters()["labels"]);
    }
}
