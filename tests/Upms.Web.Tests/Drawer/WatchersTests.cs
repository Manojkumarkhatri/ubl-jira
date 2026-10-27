using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's watch toggle and watcher list (Phase 4 FR-012, FR-013; research R25).</summary>
public sealed class WatchersTests : BunitTestBase
{
    private readonly FakeWatcherService _watchers;
    private readonly FakeMentionService _people = new();

    public WatchersTests()
    {
        _watchers = new FakeWatcherService(CurrentUser.UserId!.Value);
        _watchers.Watching.Add(new PersonView(Guid.NewGuid(), "Omar Ali", "omar"));
        Services.AddSingleton<IWatcherService>(_watchers);
        Services.AddSingleton<IMentionService>(_people);
    }

    private IRenderedComponent<WatchersSection> Show() =>
        Render<WatchersSection>(p => p.Add(x => x.WorkItemKey, "WEB-1").Add(x => x.Initial, _watchers.View()));

    [Fact]
    public void P4_FR012_Watch_and_stop_watching_with_the_count()
    {
        var cut = Show();
        Assert.Equal("Watch", cut.Find("[data-testid=watch-toggle]").TextContent.Trim());
        Assert.Equal("1 watcher", cut.Find("[data-testid=watchers-count]").TextContent.Trim());

        cut.Find("[data-testid=watch-toggle]").Click();
        cut.WaitForAssertion(() => Assert.Equal("Stop watching", cut.Find("[data-testid=watch-toggle]").TextContent.Trim()));
        Assert.Equal("2 watchers", cut.Find("[data-testid=watchers-count]").TextContent.Trim());
        Assert.Equal("You are watching WEB-1.", cut.Find("[data-testid=watchers] [role=status]").TextContent);

        cut.Find("[data-testid=watch-toggle]").Click();
        cut.WaitForAssertion(() => Assert.Equal("Watch", cut.Find("[data-testid=watch-toggle]").TextContent.Trim()));
        Assert.Equal(["watch", "stop"], _watchers.Calls);
    }

    [Fact]
    public void P4_FR012_The_count_opens_the_list_of_watchers()
    {
        var cut = Show();
        Assert.Empty(cut.FindAll("#watchers-list"));

        cut.Find("[data-testid=watchers-count]").Click();

        Assert.Equal("true", cut.Find("[data-testid=watchers-count]").GetAttribute("aria-expanded"));
        Assert.Equal(["Omar Ali"], cut.FindAll("#watchers-list li span").Select(s => s.TextContent));
        Assert.Empty(cut.FindAll("#watchers-list button"));
        Assert.Empty(cut.FindAll("#add-watcher"));
    }

    [Fact]
    public void P4_FR013_Project_Admins_add_and_remove_watchers()
    {
        _watchers.CanManage = true;
        var cut = Show();
        cut.Find("[data-testid=watchers-count]").Click();

        cut.Find("#add-watcher").Input("bil");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".watcher-choices button")));
        cut.Find(".watcher-choices button").Click();
        cut.WaitForAssertion(() => Assert.Equal("2 watchers", cut.Find("[data-testid=watchers-count]").TextContent.Trim()));

        cut.Find("[aria-label='Remove Omar Ali from watchers']").Click();
        cut.WaitForAssertion(() => Assert.Equal("1 watcher", cut.Find("[data-testid=watchers-count]").TextContent.Trim()));
        Assert.Equal($"add {_people.People[0].UserId}", _watchers.Calls[0]);
        Assert.StartsWith("remove ", _watchers.Calls[1], StringComparison.Ordinal);
    }
}
