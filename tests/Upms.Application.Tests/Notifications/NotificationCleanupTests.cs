using Upms.Application.Notifications;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Notifications;

/// <summary>Notifications are kept for 90 days (Phase 4 FR-021, research R9).</summary>
public sealed class NotificationCleanupTests(SqlServerFixture fixture) : NotificationTestBase(fixture)
{
    [Fact]
    public async Task Notifications_older_than_90_days_are_deleted_and_newer_ones_kept()
    {
        var bilal = await MemberAsync("bilal");
        await CreateAsync(WorkItemType.Task, "Plan the launch");
        var (project, item) = (await ProjectIdAsync(), await ItemIdAsync("WEB-1"));
        await RaiseAsync(new NotificationEvent(NotificationKind.Commented, project, item, Owner.Id, [bilal.Id]));
        Harness.Time.Advance(TimeSpan.FromDays(10));
        await RaiseAsync(new NotificationEvent(NotificationKind.Mentioned, project, item, Owner.Id, [bilal.Id]));

        Harness.Time.Advance(TimeSpan.FromDays(80) + TimeSpan.FromMinutes(1));
        var deleted = await CallAsync<INotificationCleanup, int>(c => c.RunAsync(Ct));

        Assert.Equal(1, deleted);
        Assert.Equal(NotificationKind.Mentioned, Assert.Single(await NotificationRowsAsync()).Kind);
    }
}
