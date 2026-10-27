using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Notifications;

/// <summary>Who hears about work on an item (Phase 4 FR-011–FR-017): watchers start by themselves, mentions and
/// assignments reach their person, and nobody is told about their own actions.</summary>
public sealed class WorkNotificationTests(SqlServerFixture fixture) : NotificationTestBase(fixture)
{
    private User _bilal = null!;
    private User _omar = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        _omar = await MemberAsync("omar");
        ActAs(Owner);
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    private Task<List<(string Item, Guid Recipient, NotificationKind Kind, string? Detail)>> NotifiedAsync() =>
        QueryAsync(db => db.Notifications.AsNoTracking()
            .Join(db.WorkItems.IgnoreQueryFilters(), n => n.WorkItemId, w => w.Id, (n, w) => new { n.Id, w.Key, n.RecipientId, n.Kind, n.Detail })
            .OrderBy(n => n.Id)
            .Select(n => ValueTuple.Create(n.Key, n.RecipientId, n.Kind, n.Detail))
            .ToListAsync(Ct));

    private Task<List<Guid>> WatchersOfAsync(string key) =>
        QueryAsync(db => db.Watches.AsNoTracking()
            .Where(w => db.WorkItems.Any(i => i.Id == w.WorkItemId && i.Key == key))
            .Select(w => w.UserId)
            .ToListAsync(Ct));

    private async Task<CommentView> CommentAsync(User author, string document, string key = "WEB-1")
    {
        ActAs(author);
        return (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync(key, document, Ct))).ValueOrThrow();
    }

    private async Task WatchAsync(User user, string key = "WEB-1")
    {
        ActAs(user);
        Assert.True((await CallAsync<IWatcherService, Result<WatchersView>>(s => s.WatchAsync(key, Ct))).IsSuccess);
    }

    [Fact]
    public async Task P4_US1_AS1_Posting_a_comment_makes_its_author_a_watcher()
    {
        await CommentAsync(_bilal, Of(Paragraph(Run("Looks "), Run("good", "bold"))));

        Assert.Contains(_bilal.Id, await WatchersOfAsync("WEB-1"));
    }

    [Fact]
    public async Task P4_US1_AS6_A_mention_notifies_the_mentioned_person()
    {
        await CommentAsync(Owner, Mentioning("Can you check this ", _bilal));

        Assert.Equal([("WEB-1", _bilal.Id, NotificationKind.Mentioned, (string?)null)], await NotifiedAsync());
    }

    [Fact]
    public async Task P4_US1_AS9_Watchers_hear_about_comments_but_the_author_does_not()
    {
        await WatchAsync(_omar);

        await CommentAsync(_bilal, Text("Venue booked"));

        Assert.Equal(new[] { Owner.Id, _omar.Id }.Order(), (await NotifiedAsync()).Select(n => n.Recipient).Order());
        Assert.All(await NotifiedAsync(), n => Assert.Equal(NotificationKind.Commented, n.Kind));
    }

    [Fact]
    public async Task P4_US1_AS9_Watchers_hear_about_status_changes_from_the_drawer()
    {
        await WatchAsync(_omar);
        ActAs(_bilal);

        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Status(InProgress))).IsSuccess);

        Assert.Equal(new[] { ("WEB-1", Owner.Id, NotificationKind.StatusChanged, (string?)"In Progress"), ("WEB-1", _omar.Id, NotificationKind.StatusChanged, "In Progress") }
            .OrderBy(n => n.Item2), (await NotifiedAsync()).OrderBy(n => n.Recipient));
    }

    [Fact]
    public async Task P4_US1_AS9_Watchers_hear_about_status_changes_from_the_board()
    {
        await WatchAsync(_omar);
        ActAs(_omar);
        var card = (await BoardAsync()).Columns[0].Cards.Single();

        Assert.True((await MoveAsync(card, Done, CardPlacement.AtTop)).IsSuccess);

        Assert.Equal([("WEB-1", Owner.Id, NotificationKind.StatusChanged, (string?)"Done")], await NotifiedAsync());
    }

    [Fact]
    public async Task P4_US1_AS9_Watchers_hear_when_a_sub_task_is_marked_done()
    {
        var parent = await AddSubtaskAsync("WEB-1", "Pick the photo");
        ActAs(_bilal);

        Assert.True((await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.MarkSubtaskDoneAsync("WEB-2", parent.Subtasks.Items.Single().Version, Ct))).IsSuccess);

        Assert.Equal([("WEB-2", Owner.Id, NotificationKind.StatusChanged, (string?)"Done")], await NotifiedAsync());
    }

    [Fact]
    public async Task P4_US1_AS9_Watchers_hear_when_deleting_a_column_moves_their_item()
    {
        var columns = (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.GetAsync("WEB", Ct))).ValueOrThrow();
        var review = (await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s =>
            s.AddAsync("WEB", "Review", StatusCategory.InProgress, 2, columns.BoardVersion, Ct))).ValueOrThrow();
        var reviewId = review.Columns.Single(c => c.Name == "Review").Id;
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Status(reviewId))).IsSuccess);
        await CommentAsync(_bilal, Text("Reviewing"));
        var before = (await NotifiedAsync()).Count;
        ActAs(Owner);

        Assert.True((await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s =>
            s.DeleteAsync("WEB", reviewId, ToDo, review.BoardVersion, Ct))).IsSuccess);

        Assert.Equal([("WEB-1", _bilal.Id, NotificationKind.StatusChanged, (string?)"To Do")], (await NotifiedAsync()).Skip(before));
    }

    [Fact]
    public async Task Deleting_a_comment_notifies_no_one()
    {
        await WatchAsync(_omar);
        var comment = await CommentAsync(Owner, Text("Wrong item"));
        var before = (await NotifiedAsync()).Count;

        Assert.True((await CallAsync<ICommentService, Result>(s => s.DeleteAsync(comment.Id, Ct))).IsSuccess);

        Assert.Equal(before, (await NotifiedAsync()).Count);
    }

    [Fact]
    public async Task P4_US1_AS10_Assigning_someone_else_notifies_them_and_makes_them_watch()
    {
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(_bilal.Id))).IsSuccess);

        Assert.Equal([("WEB-1", _bilal.Id, NotificationKind.Assigned, (string?)null)], await NotifiedAsync());
        Assert.Contains(_bilal.Id, await WatchersOfAsync("WEB-1"));
    }

    [Fact]
    public async Task P4_US1_AS10_Assigning_oneself_notifies_no_one()
    {
        ActAs(_omar);

        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(_omar.Id))).IsSuccess);

        Assert.Empty(await NotifiedAsync());
        Assert.Contains(_omar.Id, await WatchersOfAsync("WEB-1"));
    }

    [Fact]
    public async Task P4_US1_AS11_A_watcher_who_is_mentioned_gets_one_notification()
    {
        await WatchAsync(_omar);

        await CommentAsync(_bilal, Mentioning("Over to you ", _omar));

        var forOmar = (await NotifiedAsync()).Where(n => n.Recipient == _omar.Id).ToList();
        Assert.Equal(NotificationKind.Mentioned, Assert.Single(forOmar).Kind);
    }

    [Fact]
    public async Task P4_US1_AS14_Someone_removed_from_the_project_hears_nothing_until_added_back()
    {
        await WatchAsync(_omar);
        await CommentAsync(_bilal, Text("First"));
        var web = await ProjectIdAsync();
        await Data.RemoveMemberAsync(web, _omar.Id);

        await CommentAsync(_bilal, Text("Second"));
        ActAs(_omar);
        Assert.Equal(0, await UnreadAsync());
        Assert.Empty((await ListAsync()).Items);
        Assert.Contains(_omar.Id, await WatchersOfAsync("WEB-1"));

        await Data.MemberAsync(web, _omar.Id);
        await CommentAsync(_bilal, Text("Third"));

        Assert.Equal(2, (await NotifiedAsync()).Count(n => n.Recipient == _omar.Id));
        ActAs(_omar);
        Assert.Equal(2, await UnreadAsync());
    }
}
