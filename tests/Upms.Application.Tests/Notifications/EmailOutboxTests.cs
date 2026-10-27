using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Notifications;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Notifications;

/// <summary>Which notifications also queue an email (Phase 4 FR-049–FR-051, research R10, R11): by each person's choice,
/// never about their own actions, and nothing while email is off.</summary>
public sealed class EmailOutboxTests(SqlServerFixture fixture) : EmailTestBase(fixture)
{
    private async Task RaiseForAsync(NotificationKind kind, params Upms.Domain.Identity.User[] recipients) =>
        await RaiseAsync(new NotificationEvent(kind, await ProjectIdAsync(), await ItemIdAsync("WEB-1"), Owner.Id, recipients.Select(r => r.Id).ToList()));

    [Fact]
    public async Task P4_US5_AS1_An_assignment_queues_an_email_with_the_default_choice()
    {
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(Bilal.Id))).IsSuccess);

        Assert.Equal([(Bilal.Id, NotificationKind.Assigned, EmailState.Pending)], await EmailsAsync());
        Assert.Equal(0, Mail.Tries); // the action never waits for the mail server (FR-052)
    }

    [Fact]
    public async Task P4_US5_AS2_A_mention_queues_an_email_with_the_default_choice()
    {
        ActAs(Owner);
        (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Mentioning("Please check, ", Bilal), Ct))).ValueOrThrow();

        Assert.Equal([(Bilal.Id, NotificationKind.Mentioned, EmailState.Pending)], await EmailsAsync());
    }

    [Theory]
    [InlineData(NotificationKind.ApprovalRequested)]
    [InlineData(NotificationKind.GateDecided)]
    [InlineData(NotificationKind.UploadBlocked)]
    public async Task P4_US5_AS3_Gate_and_upload_events_are_aimed_at_their_recipient(NotificationKind kind)
    {
        await RaiseForAsync(kind, Bilal, Farah);

        Assert.Equal([(Bilal.Id, kind, EmailState.Pending)], await EmailsAsync());
    }

    [Theory]
    [InlineData(NotificationKind.Commented)]
    [InlineData(NotificationKind.StatusChanged)]
    public async Task P4_US5_AS4_Comments_and_status_changes_on_watched_items_are_emailed_only_to_All(NotificationKind kind)
    {
        await RaiseForAsync(kind, Bilal, Omar, Farah);

        Assert.Equal([(Omar.Id, kind, EmailState.Pending)], await EmailsAsync());
        Assert.Equal(3, (await NotificationRowsAsync()).Count); // everyone still gets the in-app notification
    }

    [Fact]
    public async Task P4_US5_AS6_None_queues_nothing()
    {
        await RaiseForAsync(NotificationKind.Mentioned, Farah);

        Assert.Empty(await EmailsAsync());
        Assert.Single(await NotificationRowsAsync());
    }

    [Fact]
    public async Task P4_US5_AS7_Ones_own_actions_queue_nothing()
    {
        ActAs(Omar);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(Omar.Id))).IsSuccess);

        Assert.Empty(await EmailsAsync());
    }

    [Fact]
    public async Task Nothing_is_queued_while_email_is_off()
    {
        Mail.IsConfigured = false;

        await RaiseForAsync(NotificationKind.Mentioned, Bilal, Omar);

        Assert.Empty(await EmailsAsync());
        Assert.Equal(2, (await NotificationRowsAsync()).Count);
    }

    [Fact]
    public async Task Deactivated_people_get_no_email()
    {
        await QueryAsync(db => db.Users.Where(u => u.Id == Bilal.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), Ct));

        await RaiseForAsync(NotificationKind.Assigned, Bilal, Omar);

        Assert.Equal([Omar.Id], (await EmailsAsync()).Select(e => e.Recipient));
    }

    [Fact]
    public async Task P4_US5_AS9_Emails_for_someone_deactivated_since_are_cancelled_when_sending()
    {
        await RaiseForAsync(NotificationKind.Assigned, Bilal);
        await QueryAsync(db => db.Users.Where(u => u.Id == Bilal.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), Ct));

        Assert.Equal(1, await DispatchAsync());

        Assert.Equal([(Bilal.Id, NotificationKind.Assigned, EmailState.Cancelled)], await EmailsAsync());
        Assert.Empty(Mail.Sent);
    }
}
