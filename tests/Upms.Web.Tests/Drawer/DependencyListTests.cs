using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's "Waits for" and "Blocks" lists and the "Add dependency" dialog (Phase 3 FR-047–FR-049). WEB-1 is
/// the item shown.</summary>
public sealed class DependencyListTests : BunitTestBase
{
    private static readonly DateOnly Nov10 = new(2026, 11, 10);
    private static readonly DateOnly Nov12 = new(2026, 11, 12);
    private readonly FakeWorkItemService _items = new();
    private readonly FakeDependencyService _dependencies = new();
    private readonly List<string> _announced = [];

    public DependencyListTests()
    {
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<IDependencyService>(_dependencies);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        _items.Details = FakeWorkItemService.Sample() with
        {
            Dependencies = new DependencyList(
                [new DependencyView("WEB-5", WorkItemType.Task, "Build the API", FakeWorkItemService.ToDo, null, Nov12, IsLate: true)],
                [new DependencyView("WEB-7", WorkItemType.Task, "Launch", FakeWorkItemService.ToDo, Nov12, null, IsLate: false)]),
        };
    }

    private IRenderedComponent<TaskDrawer> Open() => Render<TaskDrawer>(p => p.Add(x => x.WorkItemKey, "WEB-1"));

    [Fact]
    public void P3_FR049_The_drawer_lists_what_the_item_waits_for_and_what_waits_for_it_with_late_marks()
    {
        var cut = Open();

        var waitsFor = cut.Find("[data-testid=waits-for] [data-testid=dependency]");
        Assert.Contains("WEB-5", waitsFor.TextContent, StringComparison.Ordinal);
        Assert.Contains("Build the API", waitsFor.TextContent, StringComparison.Ordinal);
        Assert.Contains("Due 12 Nov 2026", waitsFor.TextContent, StringComparison.Ordinal);
        Assert.Equal("Late", waitsFor.QuerySelector("[data-testid=late]")!.TextContent.Trim());
        var blocks = cut.Find("[data-testid=blocks] [data-testid=dependency]");
        Assert.Contains("WEB-7 Launch", blocks.TextContent, StringComparison.Ordinal);
        Assert.Null(blocks.QuerySelector("[data-testid=late]"));
    }

    [Fact]
    public void P3_US4_AS5_A_dependency_is_added_by_finding_the_other_item()
    {
        var cut = Open();

        cut.Find("[data-testid=add-dependency]").Click();
        cut.Find("#dependency-search").Input("app");
        Assert.Equal(["WEB-6 Build the app"], cut.FindAll("[data-testid=dependency-result]").Select(b => b.TextContent.Trim()));
        cut.FindAll("[data-testid=dependency-result]")[0].Click();

        Assert.Equal([("WEB-1", "WEB-6")], _dependencies.Added);
        Assert.Contains("WEB-1 now waits for WEB-6.", _announced);
    }

    [Fact]
    public void An_item_that_this_one_blocks_is_added_the_other_way_round()
    {
        var cut = Open();

        cut.Find("[data-testid=add-dependency]").Click();
        cut.Find("#dependency-direction").Change("blocks");
        cut.Find("#dependency-search").Input("WEB-6");
        cut.FindAll("[data-testid=dependency-result]")[0].Click();

        Assert.Equal([("WEB-6", "WEB-1")], _dependencies.Added);
    }

    [Fact]
    public void P3_US4_AS7_A_refused_dependency_is_explained_and_the_search_is_kept()
    {
        _dependencies.NextError = AppError.Rule(ErrorCodes.DependencyLoop, "This would create a loop: the other item already waits for this one, directly or through other items.");
        var cut = Open();

        cut.Find("[data-testid=add-dependency]").Click();
        cut.Find("#dependency-search").Input("app");
        cut.FindAll("[data-testid=dependency-result]")[0].Click();

        Assert.Contains("would create a loop", cut.Find("[data-testid=dependency-error]").TextContent, StringComparison.Ordinal);
        Assert.Equal("app", cut.Find("#dependency-search").GetAttribute("value"));
    }

    [Fact]
    public void A_dependency_is_removed_and_the_removal_announced()
    {
        var cut = Open();

        cut.Find("[data-testid=waits-for] [data-testid=remove-dependency]").Click();

        Assert.Equal([("WEB-1", "WEB-5")], _dependencies.Removed);
        Assert.Contains("WEB-1 no longer waits for WEB-5.", _announced);
    }

    [Fact]
    public void P3_US4_AS9_Viewers_read_the_dependencies_but_change_nothing()
    {
        _items.Details = _items.Details with { CanContribute = false };

        var cut = Open();

        Assert.NotEmpty(cut.FindAll("[data-testid=dependency]"));
        Assert.Empty(cut.FindAll("[data-testid=add-dependency]"));
        Assert.Empty(cut.FindAll("[data-testid=remove-dependency]"));
    }

    [Theory]
    [InlineData(WorkItemField.DependencyAdded, null, "waits for WEB-5 Build the API", null, "added a dependency: this task waits for WEB-5 Build the API")]
    [InlineData(WorkItemField.DependencyRemoved, "is waited for by WEB-7 Launch", null, null, "removed a dependency: this task is waited for by WEB-7 Launch")]
    [InlineData(WorkItemField.DependencyRemoved, "waits for WEB-5 Build the API", null, "deleted", "removed a dependency: this task waits for WEB-5 Build the API (the other item was deleted)")]
    public void P3_FR052_Dependency_changes_read_as_sentences_in_the_history(WorkItemField field, string? from, string? to, string? note, string expected)
    {
        _items.Details = _items.Details with
        {
            History = new Page<ChangeView>([new ChangeView(DateTimeOffset.UnixEpoch, "Amina Khan", field, from, to, note)], 1, 1, 50),
        };

        var cut = Open().ShowHistory();

        Assert.Contains($"Amina Khan {expected}", cut.Find("[data-testid=history-entry]").TextContent, StringComparison.Ordinal);
    }
}
