using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Timeline;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Timeline;

/// <summary>A new address reaches the page only after a round trip through the browser: a second choice of scale or
/// "Hide completed" made meanwhile keeps the first (Phase 2 FR-035, FR-040).</summary>
public sealed class TimelineAddressTimingTests : BunitTestBase
{
    private readonly SlowNavigationManager _navigation = new("projects/WEB/timeline");

    public TimelineAddressTimingTests()
    {
        Services.AddSingleton<ITimelineService>(new FakeTimelineService());
        Services.AddSingleton<IWorkItemService>(new FakeWorkItemService());
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddSingleton(new LiveAnnouncer());
        Services.AddSingleton<NavigationManager>(_navigation);
    }

    [Fact]
    public void Hide_completed_chosen_before_a_new_scale_arrives_keeps_the_scale()
    {
        var cut = Render<TimelinePage>(p => p.Add(x => x.Key, "WEB"));
        _navigation.Holding = true;

        cut.Find("[data-scale=weeks]").Click();
        cut.Find("[data-testid=hide-completed]").Click();
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/projects/WEB/timeline?scale=weeks&completed=hide", _navigation.Uri, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("[data-scale=weeks]").GetAttribute("aria-pressed"));
        Assert.Equal("true", cut.Find("[data-testid=hide-completed]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_second_scale_chosen_before_the_first_arrives_wins()
    {
        var cut = Render<TimelinePage>(p => p.Add(x => x.Key, "WEB"));
        _navigation.Holding = true;

        cut.Find("[data-scale=weeks]").Click();
        cut.Find("[data-testid=hide-completed]").Click();
        cut.Find("[data-scale=quarters]").Click();
        cut.InvokeAsync(_navigation.Arrive);

        Assert.EndsWith("/projects/WEB/timeline?scale=quarters&completed=hide", _navigation.Uri, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("[data-scale=quarters]").GetAttribute("aria-pressed"));
    }
}
