using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The sub-task list in the drawer (FR-028).</summary>
public sealed class SubtaskListTests : BunitTestBase
{
    private readonly List<string> _added = [];
    private readonly List<string> _markedDone = [];
    private readonly List<(string Key, long ColumnId)> _statusChanges = [];

    private IRenderedComponent<SubtaskList> RenderList() => Render<SubtaskList>(p => p
        .Add(x => x.ProjectKey, "WEB")
        .Add(x => x.Subtasks, FakeWorkItemService.Sample().Subtasks)
        .Add(x => x.Statuses, FakeWorkItemService.Sample().Statuses)
        .Add(x => x.OnAdd, title =>
        {
            _added.Add(title);
            return Task.FromResult<string?>(null);
        })
        .Add(x => x.OnMarkDone, (SubtaskView s) => _markedDone.Add(s.Key))
        .Add(x => x.OnStatusChange, ((SubtaskView Subtask, long ColumnId) change) => _statusChanges.Add((change.Subtask.Key, change.ColumnId))));

    [Fact]
    public void US2_AS6_Sub_tasks_are_listed_with_their_key_status_and_a_link()
    {
        var cut = RenderList();

        var rows = cut.FindAll("[data-testid=subtask]");
        Assert.Equal(2, rows.Count);
        var link = rows[0].QuerySelector("a")!;
        Assert.Equal("projects/WEB/board?task=WEB-2", link.GetAttribute("href"));
        Assert.Contains("Wireframes", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Equal("3", rows[0].QuerySelector("select")!.GetAttribute("value"));
    }

    [Fact]
    public void Mark_done_is_offered_for_open_sub_tasks_only()
    {
        var cut = RenderList();

        var rows = cut.FindAll("[data-testid=subtask]");
        Assert.Null(rows[0].QuerySelector("[data-testid=mark-done]"));
        rows[1].QuerySelector("[data-testid=mark-done]")!.Click();

        Assert.Equal(["WEB-3"], _markedDone);
    }

    [Fact]
    public void Changing_a_sub_tasks_status_requests_the_change()
    {
        var cut = RenderList();

        cut.FindAll("[data-testid=subtask]")[1].QuerySelector("select")!.Change("2");

        Assert.Equal([("WEB-3", 2L)], _statusChanges);
    }

    [Fact]
    public void Typing_a_title_adds_a_sub_task()
    {
        var cut = RenderList();

        cut.Find("#add-subtask").Input("Review with marketing");
        cut.Find("#add-subtask").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Review with marketing"], _added);
        cut.WaitForAssertion(() => Assert.Equal("", cut.Find("#add-subtask").GetAttribute("value")));
    }

    [Fact]
    public void An_empty_list_says_so()
    {
        var cut = Render<SubtaskList>(p => p
            .Add(x => x.ProjectKey, "WEB")
            .Add(x => x.Subtasks, Upms.Application.Common.Page<SubtaskView>.Empty(Upms.Application.Common.PageRequest.First))
            .Add(x => x.Statuses, FakeWorkItemService.Sample().Statuses)
            .Add(x => x.OnAdd, _ => Task.FromResult<string?>(null)));

        Assert.Contains("No sub-tasks yet", cut.Markup, StringComparison.Ordinal);
    }
}
