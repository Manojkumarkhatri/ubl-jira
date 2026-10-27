using Upms.Application.Common.Results;
using Upms.Application.Notifications;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Notifications;

/// <summary>The bell and its panel: one's own notifications only, newest first, only while the project can be seen
/// (Phase 4 FR-018–FR-020, research R8, R9).</summary>
public sealed class NotificationServiceTests(SqlServerFixture fixture) : NotificationTestBase(fixture)
{
    private User _bilal = null!;
    private long _web;
    private long _item;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        await CreateAsync(WorkItemType.Task, "Plan the launch");
        _web = await ProjectIdAsync();
        _item = await ItemIdAsync("WEB-1");
    }

    private Task CommentedAsync(Guid recipient, long? item = null, long? project = null) =>
        RaiseAsync(new NotificationEvent(NotificationKind.Commented, project ?? _web, item ?? _item, Owner.Id, [recipient]));

    [Fact]
    public async Task One_sees_only_ones_own_notifications_newest_first_20_at_a_time()
    {
        var omar = await MemberAsync("omar");
        for (var i = 0; i < 25; i++)
        {
            await CommentedAsync(_bilal.Id);
            Harness.Time.Advance(TimeSpan.FromMinutes(1));
        }

        await CommentedAsync(omar.Id);
        ActAs(_bilal);

        var first = await ListAsync();
        var second = await ListAsync(page: 2);

        Assert.Equal(25, await UnreadAsync());
        Assert.Equal((25, 20, 5), (first.TotalCount, first.Items.Count, second.Items.Count));
        Assert.True(first.Items.Zip(first.Items.Skip(1)).All(p => p.First.OccurredAt > p.Second.OccurredAt));
        Assert.True(first.Items[^1].OccurredAt > second.Items[0].OccurredAt);
        ActAs(omar);
        Assert.Equal(1, await UnreadAsync());
    }

    [Fact]
    public async Task A_notification_shows_the_actor_and_the_items_current_key_and_title()
    {
        await RaiseAsync(new NotificationEvent(NotificationKind.StatusChanged, _web, _item, Owner.Id, [_bilal.Id], "Done"));
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Title("Plan the big launch"))).IsSuccess);
        ActAs(_bilal);

        var shown = Assert.Single((await ListAsync()).Items);

        Assert.Equal((NotificationKind.StatusChanged, "Amina Tester", "WEB", "WEB-1", "Plan the big launch", "Done", false),
            (shown.Kind, shown.ActorName, shown.ProjectKey, shown.ItemKey, shown.ItemTitle, shown.Detail, shown.IsRead));
    }

    [Fact]
    public async Task P4_US1_AS14_Notifications_about_a_project_one_left_are_neither_counted_nor_listed()
    {
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Mobile App", "MOB", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
        await Data.MembersAsync("MOB", ProjectRole.Member, _bilal);
        await CreateAsync(WorkItemType.Task, "Ship the app", projectKey: "MOB");
        var mob = await ProjectIdAsync("MOB");
        await CommentedAsync(_bilal.Id);
        await CommentedAsync(_bilal.Id, await ItemIdAsync("MOB-1"), mob);

        await Data.RemoveMemberAsync(mob, _bilal.Id);
        ActAs(_bilal);

        Assert.Equal(1, await UnreadAsync());
        Assert.Equal(["WEB-1"], (await ListAsync()).Items.Select(n => n.ItemKey));
    }

    [Fact]
    public async Task Notifications_about_a_deleted_item_stay_listed()
    {
        await CommentedAsync(_bilal.Id);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).IsSuccess);
        ActAs(_bilal);

        Assert.Equal(1, await UnreadAsync());
        Assert.Equal("Plan the launch", Assert.Single((await ListAsync()).Items).ItemTitle);
    }

    [Fact]
    public async Task One_marks_ones_own_notifications_read_or_unread_and_all_as_read()
    {
        var omar = await MemberAsync("omar");
        await CommentedAsync(_bilal.Id);
        await CommentedAsync(_bilal.Id);
        await CommentedAsync(omar.Id);
        ActAs(_bilal);
        var mine = (await ListAsync()).Items;

        Assert.True((await CallAsync<INotificationService, Result>(s => s.MarkAsync(mine[0].Id, true, Ct))).IsSuccess);
        Assert.Equal(1, await UnreadAsync());
        Assert.True((await ListAsync()).Items.Single(n => n.Id == mine[0].Id).IsRead);
        Assert.True((await CallAsync<INotificationService, Result>(s => s.MarkAsync(mine[0].Id, false, Ct))).IsSuccess);
        Assert.Equal(2, await UnreadAsync());

        Assert.True((await CallAsync<INotificationService, Result>(s => s.MarkAllReadAsync(Ct))).IsSuccess);
        Assert.Equal(0, await UnreadAsync());
        ActAs(omar);
        Assert.Equal(1, await UnreadAsync());
    }

    [Fact]
    public async Task Someone_elses_notification_is_not_found()
    {
        await CommentedAsync(_bilal.Id);
        ActAs(_bilal);
        var id = Assert.Single((await ListAsync()).Items).Id;
        ActAs(await MemberAsync("omar"));

        var marked = await CallAsync<INotificationService, Result>(s => s.MarkAsync(id, true, Ct));

        Assert.Equal(ErrorKind.NotFound, marked.Error!.Kind);
        Assert.Null((await NotificationRowsAsync()).Single().ReadAt);
    }
}
