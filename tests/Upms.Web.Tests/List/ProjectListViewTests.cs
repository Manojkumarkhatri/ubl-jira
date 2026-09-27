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

/// <summary>The List view (Phase 2 FR-027–FR-033). The viewer is Amina Khan; "today" is 27 Sep 2026.</summary>
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
        _navigation.NavigateTo($"projects/WEB/list{query}");
        return Render<ProjectListView>(p => p.Add(x => x.Key, "WEB"));
    }

    private string Query => new Uri(_navigation.Uri).Query;

    [Fact]
    public void P2_US3_AS1_The_table_shows_every_column_with_parent_keys_and_the_count()
    {
        var cut = RenderList();

        Assert.Equal(["Key", "Title", "Status", "Priority", "Assignee", "Start", "Due", "Updated"],
            cut.FindAll("thead th").Select(th => th.TextContent.Trim()));
        var rows = cut.FindAll("[data-testid=list-row]");
        Assert.Equal(["WEB-3", "WEB-2", "WEB-1"], rows.Select(r => r.GetAttribute("data-key")));
        Assert.Contains("Sub-task of WEB-1", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Bilal Ahmed", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal("3 tasks", cut.Find("[data-testid=list-count]").TextContent.Trim());
        Assert.Equal(new DateOnly(2026, 9, 27), _list.Queries[^1].Today);
        Assert.Equal("page", cut.Find("nav.project-nav a[href='projects/WEB/list']").GetAttribute("aria-current"));
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
        Assert.Equal("inprogress", cut.Find("#filter-type").GetAttribute("value"));
        Assert.Equal("me", cut.Find("#filter-assignee").GetAttribute("value"));
        Assert.Equal("overdue", cut.Find("#filter-due").GetAttribute("value"));
        Assert.Equal("login", cut.Find("#filter-text").GetAttribute("value"));
        Assert.Equal(["Status type: In progress", "Assignee: Me", "Due: Overdue", "Words: login"],
            cut.FindAll("[data-testid=filter-chip] .chip-label").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void P2_US3_AS3_Choosing_a_filter_puts_it_in_the_address_and_starts_at_page_one()
    {
        var cut = RenderList("?page=3");

        cut.Find("#filter-type").Change("inprogress");

        Assert.Equal("?type=inprogress", Query);
        Assert.Equal([StatusCategory.InProgress], _list.Queries[^1].Query.Categories!);
        cut.Find("#filter-assignee").Change("me");
        Assert.Equal("?type=inprogress&assignee=me", Query);
    }

    [Fact]
    public void P2_US3_AS3_Each_filter_can_be_removed_on_its_own_or_all_cleared()
    {
        var cut = RenderList("?type=inprogress&assignee=me&sort=title");

        cut.FindAll("[data-testid=filter-chip] button").First(b => b.GetAttribute("aria-label") == "Remove filter Assignee: Me").Click();
        Assert.Equal("?type=inprogress&sort=title", Query);

        cut.Find("[data-testid=clear-filters]").Click();
        Assert.Equal("?sort=title", Query);
        Assert.Empty(cut.FindAll("[data-testid=filter-chip]"));
    }

    [Fact]
    public void Searching_puts_the_words_in_the_address()
    {
        var cut = RenderList();

        cut.Find("#filter-text").Input("release notes");
        cut.Find("form.list-search").Submit();

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
        Assert.Equal("projects/WEB/list?due=overdue&task=WEB-1", cut.Find("[data-key='WEB-1'] a.task-link").GetAttribute("href"));

        _navigation.NavigateTo("projects/WEB/list?due=overdue&task=WEB-1");
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

        var box = cut.Find("input.inline-input");
        box.Input("Plan the launch");
        box.KeyDown("Enter");

        Assert.Equal((FakeWorkItemListService.ToDo.Id, "Plan the launch"), Assert.Single(_boards.Created));
        Assert.Equal(loads + 1, _list.Queries.Count);
    }

    [Fact]
    public void Viewers_get_no_box_for_new_tasks()
    {
        _list.CanContribute = false;

        var cut = RenderList();

        Assert.Empty(cut.FindAll("input.inline-input"));
    }

    [Fact]
    public void P2_US3_AS7_When_nothing_matches_the_list_says_so_and_offers_to_clear_the_filters()
    {
        _list.Rows = [];

        var cut = RenderList("?due=overdue");

        Assert.Contains("No task matches these filters.", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid=empty-clear-filters]").Click();
        Assert.Equal("", Query);
    }

    [Fact]
    public void An_empty_project_invites_the_first_task()
    {
        _list.Rows = [];

        var cut = RenderList();

        Assert.Contains("No tasks yet", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=empty-clear-filters]"));
    }

    [Fact]
    public void Long_lists_are_paged_with_the_page_in_the_address()
    {
        _list.TotalCount = 120;

        var cut = RenderList();
        Assert.Equal("120 tasks", cut.Find("[data-testid=list-count]").TextContent.Trim());

        cut.FindAll("nav.pager button").Single(b => b.TextContent.Trim() == "Next").Click();

        Assert.Equal("?page=2", Query);
        Assert.Equal(2, _list.Queries[^1].Query.Page);
    }

    [Fact]
    public void An_unknown_project_shows_not_found()
    {
        _navigation.NavigateTo("projects/NOPE/list");
        var cut = Render<ProjectListView>(p => p.Add(x => x.Key, "NOPE"));

        Assert.Contains("Not found", cut.Markup, StringComparison.Ordinal);
    }
}
