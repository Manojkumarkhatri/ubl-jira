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
    private readonly SlowNavigationManager _navigation = new("projects/WEB/list");

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

    private IRenderedComponent<ProjectListView> RenderList() => Render<ProjectListView>(p => p.Add(x => x.Key, "WEB"));

    private static List<string> Chips(IRenderedComponent<ProjectListView> cut) =>
        cut.FindAll("[data-testid=filter-chip] .chip-label").Select(c => c.TextContent.Trim()).ToList();

    [Fact]
    public void A_second_filter_chosen_before_the_first_address_arrives_keeps_the_first()
    {
        var cut = RenderList();
        _navigation.Holding = true;

        cut.Find("#filter-type").Change("inprogress");
        cut.Find("#filter-assignee").Change("me");
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/projects/WEB/list?type=inprogress&assignee=me", _navigation.Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Equal(["Status type: In progress", "Assignee: Me"], Chips(cut)));
    }

    [Fact]
    public void Removing_a_chip_before_the_last_choice_arrives_keeps_that_choice()
    {
        _navigation.NavigateTo("projects/WEB/list?type=inprogress");
        var cut = RenderList();
        _navigation.Holding = true;

        cut.Find("#filter-assignee").Change("me");
        cut.FindAll("[data-testid=filter-chip] button").Single(b => b.GetAttribute("aria-label") == "Remove filter Status type: In progress").Click();
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/projects/WEB/list?assignee=me", _navigation.Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Equal(["Assignee: Me"], Chips(cut)));
    }

    [Fact]
    public void An_answer_for_an_older_address_never_replaces_the_newer_one()
    {
        _list.FilterByType = true;
        var cut = RenderList();
        var older = _list.Hold();
        cut.Find("#filter-type").Change("done");       // its answer is slow
        var newer = _list.Hold();
        cut.Find("#filter-type").Change("inprogress");

        cut.InvokeAsync(() => newer.SetResult());
        cut.InvokeAsync(() => older.SetResult());

        cut.WaitForAssertion(() => Assert.Equal(["Status type: In progress"], Chips(cut)));
        Assert.Equal(["WEB-1"], cut.FindAll("[data-testid=list-row]").Select(r => r.GetAttribute("data-key")));
    }
}
