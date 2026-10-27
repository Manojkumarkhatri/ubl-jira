using Upms.Application.Notifications.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Notifications;

/// <summary>The one place that decides who hears about an action (Phase 4 FR-014–FR-017, research R8): one row per
/// person in the caller's unit of work, never the actor, never anyone who cannot see the project, one reason each.</summary>
public sealed class NotificationCenterTests(SqlServerFixture fixture) : NotificationTestBase(fixture)
{
    [Fact]
    public async Task One_notification_is_added_per_recipient_in_the_callers_unit_of_work()
    {
        var bilal = await MemberAsync("bilal", ProjectRole.Viewer);
        var omar = await MemberAsync("omar");
        await CreateAsync(WorkItemType.Task, "Plan the launch");
        var (project, item) = (await ProjectIdAsync(), await ItemIdAsync("WEB-1"));

        await RaiseAsync(false, new NotificationEvent(NotificationKind.Commented, project, item, Owner.Id, [bilal.Id, omar.Id]));
        Assert.Empty(await NotificationRowsAsync());

        await RaiseAsync(new NotificationEvent(NotificationKind.StatusChanged, project, item, Owner.Id, [bilal.Id, omar.Id], "Done"));

        var rows = await NotificationRowsAsync();
        Assert.Equal(new[] { bilal.Id, omar.Id }.Order(), rows.Select(n => n.RecipientId).Order());
        Assert.All(rows, n => Assert.Equal((NotificationKind.StatusChanged, project, item, (Guid?)Owner.Id, "Done", (DateTimeOffset?)null),
            (n.Kind, n.ProjectId, n.WorkItemId, n.ActorId, n.Detail, n.ReadAt)));
        Assert.All(rows, n => Assert.Equal(Harness.Time.GetUtcNow(), n.OccurredAt));
    }

    [Fact]
    public async Task The_actor_and_people_who_cannot_see_the_project_are_left_out()
    {
        var bilal = await MemberAsync("bilal");
        var outsider = await Data.UserAsync("nora");
        var dora = await Data.UserAsync("dora", isActive: false);
        await Data.MembersAsync("WEB", ProjectRole.Member, dora);
        var admin = await Data.AdministratorAsync();
        await CreateAsync(WorkItemType.Task, "Plan the launch");

        await RaiseAsync(new NotificationEvent(NotificationKind.Commented, await ProjectIdAsync(), await ItemIdAsync("WEB-1"), Owner.Id,
            [Owner.Id, bilal.Id, outsider.Id, dora.Id, admin.Id]));

        // An administrator can see every project, so they may watch and hear about any item.
        Assert.Equal(new[] { bilal.Id, admin.Id }.Order(), (await NotificationRowsAsync()).Select(n => n.RecipientId).Order());
    }

    [Fact]
    public async Task One_action_gives_each_person_one_notification_with_the_strongest_reason()
    {
        var bilal = await MemberAsync("bilal");
        var omar = await MemberAsync("omar");
        var farah = await MemberAsync("farah");
        await CreateAsync(WorkItemType.Task, "Plan the launch");
        var (project, item) = (await ProjectIdAsync(), await ItemIdAsync("WEB-1"));

        await RaiseAsync(
            new NotificationEvent(NotificationKind.Commented, project, item, Owner.Id, [bilal.Id, omar.Id, farah.Id]),
            new NotificationEvent(NotificationKind.Assigned, project, item, Owner.Id, [omar.Id]),
            new NotificationEvent(NotificationKind.Mentioned, project, item, Owner.Id, [bilal.Id, omar.Id]));

        var rows = await NotificationRowsAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(NotificationKind.Mentioned, rows.Single(n => n.RecipientId == bilal.Id).Kind);
        Assert.Equal(NotificationKind.Mentioned, rows.Single(n => n.RecipientId == omar.Id).Kind);
        Assert.Equal(NotificationKind.Commented, rows.Single(n => n.RecipientId == farah.Id).Kind);
    }

    [Fact]
    public async Task Nothing_is_added_when_nobody_is_left()
    {
        await CreateAsync(WorkItemType.Task, "Plan the launch");

        await RaiseAsync(new NotificationEvent(NotificationKind.Commented, await ProjectIdAsync(), await ItemIdAsync("WEB-1"), Owner.Id, [Owner.Id]));
        await RaiseAsync();

        Assert.Empty(await NotificationRowsAsync());
    }
}
