using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Pages.List;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.List;

/// <summary>In the browser a new address reaches the page only after a round trip, so choices made meanwhile must build
/// on the ones before them, and an answer for an older address must not replace a newer one (Phase 2 FR-030).</summary>
public sealed class ListAddressTimingTests : BunitTestBase
{
    private readonly FakeWorkItemListService _list;
    private readonly SlowNavigationManager _navigation = new("spaces/WEB/list");

    public ListAddressTimingTests()
    {
        _list = new FakeWorkItemListService(CurrentUser.UserId!.Value);
        Services.AddSingleton<IWorkItemListService>(_list);
        Services.AddSingleton<IBoardService>(new FakeBoardService());
        Services.AddSingleton<IWorkItemService>(new FakeWorkItemService());
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        Services.AddSingleton<NavigationManager>(_navigation);
    }

    private IRenderedComponent<ProjectListView> RenderList()
    {
        var cut = Render<ProjectListView>(p => p.Add(x => x.Key, "WEB"));
        cut.Find("button[aria-controls=list-filter-panel]").Click();
        return cut;
    }

    /// <summary>A Status button of the Filter panel.</summary>
    private static AngleSharp.Dom.IElement Status(IRenderedComponent<ProjectListView> cut, string name) =>
        cut.FindAll("#list-filter-panel button").Single(b => b.TextContent.Trim() == name);

    private static AngleSharp.Dom.IElement Me(IRenderedComponent<ProjectListView> cut) => cut.Find(".avatar-filter button[title='Amina Khan (you)']");

    private static List<string> Pressed(IRenderedComponent<ProjectListView> cut) =>
        cut.FindAll("[aria-pressed=true]").Select(b => (b.QuerySelector(".sr-only")?.TextContent ?? b.TextContent).Trim()).ToList();

    [Fact]
    public void A_second_filter_chosen_before_the_first_address_arrives_keeps_the_first()
    {
        var cut = RenderList();
        _navigation.Holding = true;

        Status(cut, "In Progress").Click();
        Me(cut).Click();
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/spaces/WEB/list?column=2&assignee=me", _navigation.Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Equal(["Assigned to me", "In Progress"], Pressed(cut)));
    }

    [Fact]
    public void Removing_a_filter_before_the_last_choice_arrives_keeps_that_choice()
    {
        _navigation.NavigateTo("spaces/WEB/list?column=2");
        var cut = RenderList();
        _navigation.Holding = true;

        Me(cut).Click();
        Status(cut, "In Progress").Click();
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/spaces/WEB/list?assignee=me", _navigation.Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Equal(["Assigned to me"], Pressed(cut)));
    }

    [Fact]
    public void An_answer_for_an_older_address_never_replaces_the_newer_one()
    {
        _list.FilterByStatus = true;
        var cut = RenderList();
        var older = _list.Hold();
        Status(cut, "Done").Click();          // its answer is slow
        var newer = _list.Hold();
        Status(cut, "In Progress").Click();   // Done and In Progress

        cut.InvokeAsync(() => newer.SetResult());
        cut.InvokeAsync(() => older.SetResult());

        cut.WaitForAssertion(() => Assert.Equal(["In Progress", "Done"], Pressed(cut)));
        Assert.Equal(["WEB-2", "WEB-1"], cut.FindAll("[data-testid=list-row]").Select(r => r.GetAttribute("data-key")));
    }
}
