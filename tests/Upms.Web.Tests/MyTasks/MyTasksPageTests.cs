using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.MyTasks;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.MyTasks;

/// <summary>The "My tasks" page (Phase 2 FR-025, FR-026). "Today" is 27 Sep 2026.</summary>
public sealed class MyTasksPageTests : BunitTestBase
{
    private readonly FakeMyTasksService _tasks = new();
    private readonly FakeWorkItemService _items = new();

    public MyTasksPageTests()
    {
        Services.AddSingleton<IMyTasksService>(_tasks);
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<ViewerToday>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
    }

    private IRenderedComponent<MyTasksPage> RenderPage() => Render<MyTasksPage>();

    [Fact]
    public void P2_US2_AS7_Rows_are_grouped_by_project_with_overdue_marks_and_parent_keys()
    {
        var cut = RenderPage();

        Assert.Equal("My tasks", cut.Find("h1").TextContent.Trim());
        Assert.Equal(["Payroll", "Website Revamp"], cut.FindAll("h2.group-title").Select(h => h.TextContent.Split('(')[0].Trim()));
        var groups = cut.FindAll("[data-testid=my-tasks-group]");
        Assert.Equal(["PAY-1"], groups[0].QuerySelectorAll("[data-testid=my-task]").Select(r => r.GetAttribute("data-key")));
        Assert.Equal(["WEB-1", "WEB-3", "WEB-7"], groups[1].QuerySelectorAll("[data-testid=my-task]").Select(r => r.GetAttribute("data-key")));
        var web1 = cut.Find("[data-key='WEB-1']");
        Assert.Contains("Overdue", web1.TextContent, StringComparison.Ordinal);
        Assert.Contains("In Progress", web1.TextContent, StringComparison.Ordinal);
        Assert.Contains("High", web1.TextContent, StringComparison.Ordinal);
        Assert.Contains("Sub-task of WEB-1", cut.Find("[data-key='WEB-3']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Overdue", cut.Find("[data-key='WEB-3']").TextContent, StringComparison.Ordinal);
        Assert.Contains("No due date", cut.Find("[data-key='WEB-7']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_project_heading_links_to_its_board_by_name()
    {
        // A link read out of context, as in a screen reader's list of links, says which project it opens.
        var cut = RenderPage();

        Assert.Equal("Website Revamp (WEB)", cut.Find("h2.group-title a[href='projects/WEB/board']").TextContent.Trim());
    }

    [Fact]
    public void The_total_count_is_shown()
    {
        var cut = RenderPage();

        Assert.Equal("4 open tasks are assigned to you.", cut.Find("[data-testid=my-tasks-count]").TextContent.Trim());
    }

    [Fact]
    public void Someone_with_nothing_assigned_sees_an_empty_state()
    {
        _tasks.Rows.Clear();

        var cut = RenderPage();

        Assert.Contains("Nothing is assigned to you", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=my-task]"));
    }

    [Fact]
    public void Rows_open_the_task_in_the_drawer()
    {
        var cut = RenderPage();

        var link = cut.Find("[data-key='WEB-1'] a.task-link");

        Assert.Equal("my-tasks?task=WEB-1", link.GetAttribute("href"));
    }

    [Fact]
    public void P2_US2_AS7_Completing_a_task_in_the_drawer_removes_it_when_the_drawer_closes()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("my-tasks?task=WEB-1");
        var cut = RenderPage();
        Assert.NotEmpty(cut.FindAll("[data-testid=drawer]"));

        cut.Find("#drawer-status").Change(FakeWorkItemService.Done.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _tasks.Rows.RemoveAll(r => r.Key == "WEB-1");
        cut.Find("button[aria-label='Close task details']").Click();

        Assert.Empty(cut.FindAll("[data-key='WEB-1']"));
        Assert.DoesNotContain("task=", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void More_than_one_page_shows_a_pager()
    {
        for (var i = 10; i < 60; i++)
        {
            _tasks.Rows.Add(new MyTaskRow("WEB", "Website Revamp", $"WEB-{i}", $"Task {i}", null,
                new StatusOption(1, "To Do", StatusCategory.ToDo), Priority.Low, null));
        }

        var cut = RenderPage();
        Assert.Equal(50, cut.FindAll("[data-testid=my-task]").Count);

        cut.FindAll("nav.pager button").Single(b => b.TextContent.Trim() == "Next").Click();

        Assert.Equal(new PageRequest(2), _tasks.Requests[^1]);
        Assert.Equal(4, cut.FindAll("[data-testid=my-task]").Count);
    }
}
