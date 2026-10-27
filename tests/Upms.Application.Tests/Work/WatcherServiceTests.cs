using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>Watching an item (Phase 4 FR-011–FR-014, research R7): automatic for the creator, assignee and commenters;
/// anyone who can see the item can start or stop; Project Admins and administrators manage the list.</summary>
public sealed class WatcherServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _bilal = null!;
    private User _omar = null!;
    private User _vera = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        _omar = await MemberAsync("omar");
        _vera = await MemberAsync("vera", ProjectRole.Viewer);
        ActAs(Owner);
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    private Task<Result<WatchersView>> CallAsync(Func<IWatcherService, Task<Result<WatchersView>>> call) =>
        CallAsync<IWatcherService, Result<WatchersView>>(call);

    private async Task<WatchersView> WatchersAsync() => (await CallAsync(s => s.GetAsync("WEB-1", Ct))).ValueOrThrow();

    [Fact]
    public async Task P4_US1_AS8_The_creator_the_assignee_and_commenters_watch_automatically()
    {
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(_bilal.Id))).IsSuccess);
        ActAs(_omar);
        Assert.True((await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Text("On it"), Ct))).IsSuccess);

        var view = await WatchersAsync();

        Assert.Equal(3, view.Count);
        Assert.Equal(["Amina Tester", "Bilal Tester", "Omar Tester"], view.People.Select(p => p.DisplayName));
        Assert.True(view.IsWatching);
        Assert.False(view.CanManage);
    }

    [Fact]
    public async Task Watching_and_stopping_are_idempotent_for_anyone_who_can_see_the_item()
    {
        ActAs(_vera);

        Assert.False((await WatchersAsync()).IsWatching);
        await CallAsync(s => s.WatchAsync("WEB-1", Ct));
        var watching = (await CallAsync(s => s.WatchAsync("WEB-1", Ct))).ValueOrThrow();
        Assert.Equal((true, 2), (watching.IsWatching, watching.Count));

        await CallAsync(s => s.StopWatchingAsync("WEB-1", Ct));
        var stopped = (await CallAsync(s => s.StopWatchingAsync("WEB-1", Ct))).ValueOrThrow();
        Assert.Equal((false, 1), (stopped.IsWatching, stopped.Count));
    }

    [Fact]
    public async Task Project_Admins_add_and_remove_active_members()
    {
        var view = (await CallAsync(s => s.AddAsync("WEB-1", _vera.Id, Ct))).ValueOrThrow();
        Assert.True(view.CanManage);
        Assert.Contains(_vera.Id, view.People.Select(p => p.UserId));

        view = (await CallAsync(s => s.RemoveAsync("WEB-1", Owner.Id, Ct))).ValueOrThrow();
        Assert.Equal([_vera.Id], view.People.Select(p => p.UserId));
    }

    [Fact]
    public async Task Only_active_members_can_be_added_as_watchers()
    {
        var nora = await Data.UserAsync("nora");
        var dora = await Data.UserAsync("dora", isActive: false);
        await Data.MembersAsync("WEB", ProjectRole.Member, dora);

        Assert.Equal(ErrorCodes.NotAMember, (await CallAsync(s => s.AddAsync("WEB-1", nora.Id, Ct))).Error!.Code);
        Assert.Equal(ErrorCodes.NotAMember, (await CallAsync(s => s.AddAsync("WEB-1", dora.Id, Ct))).Error!.Code);
    }

    [Fact]
    public async Task Members_and_Viewers_cannot_manage_watchers_and_non_members_see_nothing()
    {
        ActAs(_bilal);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync(s => s.AddAsync("WEB-1", _omar.Id, Ct))).Error!.Kind);
        ActAs(_vera);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync(s => s.RemoveAsync("WEB-1", Owner.Id, Ct))).Error!.Kind);
        ActAs(await Data.UserAsync("nora"));
        Assert.Equal(ErrorKind.NotFound, (await CallAsync(s => s.GetAsync("WEB-1", Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync(s => s.WatchAsync("WEB-1", Ct))).Error!.Kind);
    }

    [Fact]
    public async Task Administrators_who_are_not_members_manage_watchers()
    {
        ActAs(await Data.AdministratorAsync());

        var view = (await CallAsync(s => s.AddAsync("WEB-1", _omar.Id, Ct))).ValueOrThrow();

        Assert.True(view.CanManage);
        Assert.Contains(_omar.Id, view.People.Select(p => p.UserId));
    }

    [Fact]
    public async Task P4_FR014_People_who_can_no_longer_see_the_item_are_left_out_of_the_list()
    {
        await CallAsync(s => s.AddAsync("WEB-1", _omar.Id, Ct));
        await Data.RemoveMemberAsync(await QueryAsync(db => Task.FromResult(db.Projects.Single(p => p.Key == "WEB").Id)), _omar.Id);

        var view = await WatchersAsync();

        Assert.Equal([Owner.Id], view.People.Select(p => p.UserId));
        Assert.Equal(1, view.Count);
    }
}
