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

/// <summary>The "My tasks" page (Phase 2 FR-025, FR-026; Phase 3 FR-041). "Today" is 27 Sep 2026.</summary>
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
    public void P3_US3_AS3_Gates_waiting_for_the_user_come_first_and_open_the_stage()
    {
        _tasks.Gates.Add(new WaitingGateRow("CBU", "Core Banking Upgrade", "CBU-1", "Initiation", "Musa Ali",
            new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)));

        var cut = RenderPage();

        var section = cut.Find("[data-testid=gates-waiting]");
        Assert.Equal("Gates waiting for you", section.QuerySelector("h2")!.TextContent.Trim());
        var row = section.QuerySelector("[data-testid=waiting-gate]")!;
        Assert.Contains("Core Banking Upgrade", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("Requested by Musa Ali", row.TextContent, StringComparison.Ordinal);
        Assert.Equal("spaces/CBU/stages?stage=CBU-1", row.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Initiation gate", row.QuerySelector("a")!.TextContent.Trim());
        Assert.True(section.CompareDocumentPosition(cut.FindAll("[data-testid=my-tasks-group]")[0]).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
    }

    [Fact]
    public void Without_gates_waiting_the_section_is_not_shown()
    {
        var cut = RenderPage();

        Assert.Empty(cut.FindAll("[data-testid=gates-waiting]"));
    }

    [Fact]
    public void P2_US2_AS7_Rows_are_grouped_by_project_with_overdue_marks_and_parent_keys()
    {
        var cut = RenderPage();

        Assert.Equal("For you", cut.Find("h1").TextContent.Trim());
        Assert.Equal("page", cut.Find("nav[aria-label='For you'] a[href='for-you/assigned']").GetAttribute("aria-current"));
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
    public void P3_FR009_Each_row_shows_its_type_with_the_name_as_text()
    {
        _tasks.Rows[0] = _tasks.Rows[0] with { Type = WorkItemType.Epic };

        var cut = RenderPage();

        var row = cut.Find($"[data-key='{_tasks.Rows[0].Key}']");
        Assert.Equal("Epic", row.QuerySelector(".type-icon .sr-only")!.TextContent.Trim());
    }

    [Fact]
    public void Each_project_heading_links_to_its_board_by_name()
    {
        // A link read out of context, as in a screen reader's list of links, says which project it opens.
        var cut = RenderPage();

        Assert.Equal("Website Revamp (WEB)", cut.Find("h2.group-title a[href='spaces/WEB/board']").TextContent.Trim());
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

        Assert.Equal("for-you/assigned?task=WEB-1", link.GetAttribute("href"));
    }

    [Fact]
    public void P2_US2_AS7_Completing_a_task_in_the_drawer_removes_it_when_the_drawer_closes()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("for-you/assigned?task=WEB-1");
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
