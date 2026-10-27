using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.List;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.List;

/// <summary>The List view (Phase 2 FR-027–FR-033), laid out as Jira's: a search, the people, "Filter" and "Group" above
/// the table. The viewer is Amina Khan; "today" is 27 Sep 2026.</summary>
public sealed class ProjectListViewTests : BunitTestBase
{
    private readonly FakeWorkItemListService _list;
    private readonly FakeBoardService _boards = new();
    private readonly FakeWorkItemService _items = new();
    private readonly NavigationManager _navigation;

    public ProjectListViewTests()
    {
        _list = new FakeWorkItemListService(CurrentUser.UserId!.Value);
        Services.AddSingleton<IWorkItemListService>(_list);
        Services.AddSingleton<IBoardService>(_boards);
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private IRenderedComponent<ProjectListView> RenderList(string query = "")
    {
        _navigation.NavigateTo($"spaces/WEB/list{query}");
        return Render<ProjectListView>(p => p.Add(x => x.Key, "WEB"));
    }

    private string Query => new Uri(_navigation.Uri).Query;

    private static void OpenFilter(IRenderedComponent<ProjectListView> cut) => cut.Find("button[aria-controls=list-filter-panel]").Click();

    /// <summary>The Filter panel's buttons under one heading: "Work type", "Status", "Priority", "Epic" (or "Stage"),
    /// "Label" or "Due date".</summary>
    private static List<IElement> FilterButtons(IRenderedComponent<ProjectListView> cut, string heading) =>
        [.. cut.FindAll("#list-filter-panel .panel-heading").Single(h => h.TextContent.Trim() == heading).NextElementSibling!.QuerySelectorAll("button")];

    /// <summary>A filter button's words; a type's name once, without its symbol.</summary>
    private static string Name(IElement button) => (button.QuerySelector(".type-icon .sr-only")?.TextContent ?? button.TextContent).Trim();

    private static IElement FilterButton(IRenderedComponent<ProjectListView> cut, string heading, string name) =>
        FilterButtons(cut, heading).Single(b => Name(b) == name);

    private static IElement Me(IRenderedComponent<ProjectListView> cut) => cut.Find(".avatar-filter button[title='Amina Khan (you)']");

    private static string FilterButtonText(IRenderedComponent<ProjectListView> cut) =>
        cut.Find("button[aria-controls=list-filter-panel]").TextContent.Trim();

    /// <summary>Shows a column the list hides until chosen, with the column picker.</summary>
    private static void ShowColumn(IRenderedComponent<ProjectListView> cut, string label)
    {
        cut.Find("button[aria-label='Choose columns']").Click();
        cut.FindAll("#list-columns label").Single(l => l.TextContent.Trim() == label).QuerySelector("input")!.Change(true);
    }

    [Fact]
    public void P2_US3_AS1_The_table_shows_every_column_with_parent_keys_and_the_count()
    {
        var cut = RenderList();

        // Jira's default columns; the others (start date, epic, labels, work type) are chosen with the column picker.
        Assert.Equal(["Work", "Assignee", "Reporter", "Priority", "Status", "Resolution", "Created", "Updated", "Due date"],
            cut.FindAll("thead th:not(.col-check):not(.col-pick)").Select(th => th.TextContent.Replace("↓", "").Replace("↑", "").Trim()));
        var rows = cut.FindAll("[data-testid=list-row]");
        Assert.Equal(["WEB-3", "WEB-2", "WEB-1"], rows.Select(r => r.GetAttribute("data-key")));
        Assert.Equal("in WEB-1", rows[0].QuerySelector(".work-parent")!.TextContent.Trim());
        Assert.Contains("Bilal Ahmed", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal("3 of 3", cut.Find("[data-testid=list-count]").TextContent.Trim());
        Assert.Equal(new DateOnly(2026, 9, 27), _list.Queries[^1].Today);
        Assert.Equal("page", cut.Find("nav.project-nav > a[href='spaces/WEB/list']").GetAttribute("aria-current"));
    }

    [Fact]
    public void Overdue_open_rows_are_marked_in_words()
    {
        var cut = RenderList();

        Assert.Contains("Overdue", cut.Find("[data-key='WEB-1']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Overdue", cut.Find("[data-key='WEB-2']").TextContent, StringComparison.Ordinal); // done
        Assert.DoesNotContain("Overdue", cut.Find("[data-key='WEB-3']").TextContent, StringComparison.Ordinal); // due later
    }

    [Fact]
    public void P2_US3_AS4_Filters_sort_and_page_come_from_the_address()
    {
        var cut = RenderList($"?sort=due&dir=desc&type=inprogress&assignee=me&due=overdue&q=login&page=2");

        var query = _list.Queries[^1].Query;
        Assert.Equal((ListSort.DueDate, true, 2), (query.Sort, query.Descending, query.Page));
        Assert.Equal([StatusCategory.InProgress], query.Categories!);
        Assert.Equal((true, DueFilter.Overdue, "login"), (query.AssignedToMe, query.Due, query.Text));
        Assert.Equal("descending", cut.Find("th[data-sort=due]").GetAttribute("aria-sort"));
        Assert.Equal("login", cut.Find("#list-search").GetAttribute("value"));
        Assert.Equal("true", Me(cut).GetAttribute("aria-pressed"));
        Assert.Equal("Filter (2)", FilterButtonText(cut)); // the status type kept from a Phase 2 address, and the due date
        OpenFilter(cut);
        Assert.Equal(["Overdue"], FilterButtons(cut, "Due date").Where(b => b.GetAttribute("aria-pressed") == "true").Select(Name));
    }

    [Fact]
    public void P2_US3_AS3_Choosing_a_filter_puts_it_in_the_address_and_starts_at_page_one()
    {
        var cut = RenderList("?page=3");
        OpenFilter(cut);

        FilterButton(cut, "Status", "In Progress").Click();

        Assert.Equal("?column=2", Query);
        Assert.Equal([2L], _list.Queries[^1].Query.ColumnIds!);
        Assert.Equal("true", FilterButton(cut, "Status", "In Progress").GetAttribute("aria-pressed"));
        Me(cut).Click();
        Assert.Equal("?column=2&assignee=me", Query);
    }

    [Fact]
    public void P2_US3_AS3_Each_filter_can_be_removed_on_its_own_or_all_cleared()
    {
        var cut = RenderList("?column=2&assignee=me&sort=title");

        Me(cut).Click();
        Assert.Equal("?column=2&sort=title", Query);
        Assert.Equal("false", Me(cut).GetAttribute("aria-pressed"));

        OpenFilter(cut);
        cut.Find("[data-testid=clear-filters]").Click();
        Assert.Equal("?sort=title", Query);
        Assert.Equal("Filter", FilterButtonText(cut));
        Assert.Empty(cut.FindAll("[data-testid=clear-filters]"));
    }

    [Fact]
    public void Searching_puts_the_words_in_the_address()
    {
        var cut = RenderList();

        cut.Find("#list-search").Input("release notes");
        cut.Find("form.board-search").Submit();

        Assert.Equal("?q=release%20notes", Query);
        Assert.Equal("release notes", _list.Queries[^1].Query.Text);
    }

    [Fact]
    public void P2_US3_AS2_Sortable_headers_say_how_the_list_is_sorted_and_reverse_on_a_second_choice()
    {
        var cut = RenderList();
        Assert.Equal("descending", cut.Find("th[data-sort=key]").GetAttribute("aria-sort"));
        Assert.Equal("none", cut.Find("th[data-sort=due]").GetAttribute("aria-sort"));

        cut.Find("th[data-sort=due] button").Click();
        Assert.Equal("?sort=due", Query);
        Assert.Equal("ascending", cut.Find("th[data-sort=due]").GetAttribute("aria-sort"));

        cut.Find("th[data-sort=due] button").Click();
        Assert.Equal("?sort=due&dir=desc", Query);
        Assert.Equal((ListSort.DueDate, true), (_list.Queries[^1].Query.Sort, _list.Queries[^1].Query.Descending));
    }

    [Fact]
    public void P2_US3_AS5_A_row_opens_the_drawer_over_the_list_and_changes_show_in_the_list()
    {
        var cut = RenderList("?due=overdue");
        Assert.Equal("spaces/WEB/list?due=overdue&task=WEB-1", cut.Find("[data-key='WEB-1'] a.task-link").GetAttribute("href"));

        _navigation.NavigateTo("spaces/WEB/list?due=overdue&task=WEB-1");
        Assert.NotEmpty(cut.FindAll("[data-testid=drawer]"));
        var loads = _list.Queries.Count;
        cut.Find("#drawer-priority").Change(nameof(Priority.Lowest));

        Assert.Equal(loads + 1, _list.Queries.Count);
        cut.Find("button[aria-label='Close task details']").Click();
        Assert.Equal("?due=overdue", Query);
    }

    [Fact]
    public void P2_US3_AS6_What_needs_to_be_done_creates_a_task_in_the_first_to_do_column()
    {
        var cut = RenderList();
        var loads = _list.Queries.Count;

        cut.Find("button.tl-create").Click(); // "+ Create" at the foot of the list opens the box, as in Jira
        var box = cut.Find(".list-create input.inline-input");
        box.Input("Plan the launch");
        box.KeyDown("Enter");

        // Created through the work item service, which starts every item in the leftmost "to do" column (Phase 3 FR-010).
        Assert.Equal(("WEB", new NewWorkItem(WorkItemType.Task, "Plan the launch")), Assert.Single(_items.Created));
        Assert.Equal(loads + 1, _list.Queries.Count);
    }

    [Fact]
    public void Viewers_get_no_box_for_new_tasks()
    {
        _list.CanContribute = false;

        var cut = RenderList();

        Assert.Empty(cut.FindAll("button.tl-create"));
        Assert.Empty(cut.FindAll("input.inline-input"));
    }

    [Fact]
    public void P2_US3_AS7_When_nothing_matches_the_list_says_so_and_offers_to_clear_the_filters()
    {
        _list.Rows = [];

        var cut = RenderList("?due=overdue");

        Assert.Contains("No work items match these filters.", cut.Find(".list-empty").TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=empty-clear-filters]").Click();
        Assert.Equal("", Query);
    }

    [Fact]
    public void An_empty_project_invites_the_first_task()
    {
        _list.Rows = [];

        var cut = RenderList();

        Assert.Contains("No work items yet.", cut.Find(".list-empty").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=empty-clear-filters]"));
        Assert.NotEmpty(cut.FindAll("button.tl-create"));
    }

    [Fact]
    public void Long_lists_are_paged_with_the_page_in_the_address()
    {
        _list.TotalCount = 120;

        var cut = RenderList();
        Assert.Equal("3 of 120", cut.Find("[data-testid=list-count]").TextContent.Trim());
        Assert.Contains("Page 1 of 3", cut.Find(".list-pager").TextContent, StringComparison.Ordinal);

        cut.Find(".list-pager button[aria-label='Next page']").Click();

        Assert.Equal("?page=2", Query);
        Assert.Equal(2, _list.Queries[^1].Query.Page);
    }

    [Fact]
    public void An_unknown_project_shows_not_found()
    {
        _navigation.NavigateTo("spaces/NOPE/list");
        var cut = Render<ProjectListView>(p => p.Add(x => x.Key, "NOPE"));

        Assert.Contains("Not found", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void P3_US1_AS8_Rows_show_their_type_and_epic()
    {
        _list.Rows[1] = _list.Rows[1] with { Type = WorkItemType.Bug, Container = new ContainerView("WEB-7", "Card refunds", WorkItemType.Epic) };
        var cut = RenderList();

        ShowColumn(cut, "Epic");

        var bug = cut.FindAll("[data-testid=list-row]")[1];
        Assert.Equal("Bug", bug.QuerySelector(".col-work .type-icon .sr-only")!.TextContent.Trim());
        Assert.Contains("WEB-7 Card refunds", bug.QuerySelector(".col-parent [data-testid=epic-tag]")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Epic", cut.FindAll("thead th").Select(th => th.TextContent.Trim()));
    }

    [Fact]
    public void P3_US1_AS8_The_list_filters_by_type_and_epic()
    {
        var cut = RenderList();
        OpenFilter(cut);

        Assert.Equal(["Epic", "Story", "Task", "Bug", "Sub-task"], FilterButtons(cut, "Work type").Select(Name));
        Assert.Equal(["None", "Card refunds", "Statements"], FilterButtons(cut, "Epic").Select(Name));

        FilterButton(cut, "Work type", "Bug").Click();
        cut.WaitForAssertion(() => Assert.Contains("kind=bug", Query, StringComparison.Ordinal));
        FilterButton(cut, "Epic", "Card refunds").Click();
        cut.WaitForAssertion(() => Assert.Contains("container=WEB-7", Query, StringComparison.Ordinal));

        cut.WaitForAssertion(() => Assert.Equal("true", FilterButton(cut, "Epic", "Card refunds").GetAttribute("aria-pressed")));
        Assert.Equal("true", FilterButton(cut, "Work type", "Bug").GetAttribute("aria-pressed"));
        Assert.Equal("Filter (2)", FilterButtonText(cut));
        var query = _list.Queries[^1].Query;
        Assert.Equal([WorkItemType.Bug], query.Types!);
        Assert.Equal(["WEB-7"], query.ContainerKeys!);
    }

    [Fact]
    public void In_a_Stage_gate_project_the_list_offers_stages_and_milestones()
    {
        _list.Template = ProjectTemplate.StageGate;
        _items.Containers = [new ContainerOption("WEB-1", "Initiation", WorkItemType.Stage, false)];

        var cut = RenderList();
        OpenFilter(cut);

        Assert.Equal(["Stage", "Task", "Milestone", "Sub-task"], FilterButtons(cut, "Work type").Select(Name));
        Assert.Equal(["None", "Initiation"], FilterButtons(cut, "Stage").Select(Name));
        ShowColumn(cut, "Stage");
        Assert.Contains("Stage", cut.FindAll("thead th").Select(th => th.TextContent.Trim()));
    }

    [Fact]
    public void P3_FR010_The_create_box_offers_every_type_the_contributor_may_create()
    {
        var cut = RenderList();
        cut.Find("button.tl-create").Click();

        var choice = cut.Find(".list-create select.type-choice");
        Assert.Equal(["Task", "Story", "Bug", "Epic"], choice.QuerySelectorAll("option").Select(o => o.TextContent.Trim()));
        Assert.Equal("Task", choice.GetAttribute("value"));

        choice.Change("Epic");
        cut.Find(".list-create input.inline-input").Input("Card refunds");
        cut.Find(".list-create input.inline-input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        var (projectKey, item) = Assert.Single(_items.Created);
        Assert.Equal(("WEB", WorkItemType.Epic, "Card refunds", (string?)null), (projectKey, item.Type, item.Title, item.ContainerKey));
    }

    [Fact]
    public void P4_US4_AS6_Rows_show_labels_and_a_label_filter_lives_in_the_address()
    {
        _list.Rows[2] = _list.Rows[2] with { Labels = ["design", "launch"] };
        var cut = RenderList("?labels=launch");

        ShowColumn(cut, "Labels");
        Assert.Equal(["design", "launch"], cut.Find("[data-key='WEB-1'] .col-labels").QuerySelectorAll("li").Select(l => l.TextContent));
        Assert.Equal(["launch"], _list.Queries[^1].Query.Labels);
        OpenFilter(cut);
        Assert.Equal(["design", "launch"], FilterButtons(cut, "Label").Select(Name));
        Assert.Equal("true", FilterButton(cut, "Label", "launch").GetAttribute("aria-pressed"));

        cut.Find("#filter-label").Change("Pricing");
        cut.Find("#filter-label").Closest("form")!.Submit();
        Assert.Equal("?labels=launch%2CPricing", Query);

        FilterButton(cut, "Label", "launch").Click();
        Assert.Equal("?labels=Pricing", Query);
    }
}
