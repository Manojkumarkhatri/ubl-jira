using Microsoft.EntityFrameworkCore;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Notifications;

/// <summary>Gate requests reach the approvers who may decide, and outcomes reach the requester (Phase 4 FR-016).</summary>
public sealed class GateNotificationTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    private Task<List<(string Item, Guid Recipient, NotificationKind Kind, string? Detail)>> NotifiedAsync() =>
        QueryAsync(db => db.Notifications.AsNoTracking()
            .Join(db.WorkItems, n => n.WorkItemId, w => w.Id, (n, w) => new { n.Id, w.Key, n.RecipientId, n.Kind, n.Detail })
            .OrderBy(n => n.Id)
            .Select(n => ValueTuple.Create(n.Key, n.RecipientId, n.Kind, n.Detail))
            .ToListAsync(Ct));

    [Fact]
    public async Task P4_US1_AS15_A_request_notifies_each_approver_who_may_decide()
    {
        // Musa is excluded from deciding on his own request; Farah, from outside the team, becomes a Viewer and may decide.
        await SetUpAsync(Initiation, [Omar, Musa, Farah], required: 1, exclude: true);

        await RequestAsync(Initiation, Musa);

        var notified = await NotifiedAsync();
        Assert.Equal(new[] { Omar.Id, Farah.Id }.Order(), notified.Select(n => n.Recipient).Order());
        Assert.All(notified, n => Assert.Equal((Initiation, NotificationKind.ApprovalRequested), (n.Item, n.Kind)));
    }

    [Theory]
    [InlineData(GateDecisionKind.Approve, "Approved")]
    [InlineData(GateDecisionKind.SendBack, "Sent back")]
    public async Task P4_US1_AS15_The_outcome_notifies_the_requester(GateDecisionKind decision, string outcome)
    {
        await SetUpAsync(Initiation, [Omar], exclude: false);
        var request = await RequestAsync(Initiation, Musa);
        var before = (await NotifiedAsync()).Count;

        await DecideAsync(Omar, Initiation, request.Gate.CurrentRequest!.Number, decision, decision == GateDecisionKind.SendBack ? "Add the budget" : null);

        Assert.Equal([(Initiation, Musa.Id, NotificationKind.GateDecided, (string?)outcome)], (await NotifiedAsync()).Skip(before));
    }

    [Fact]
    public async Task An_approval_that_does_not_yet_decide_the_gate_notifies_no_one()
    {
        await SetUpAsync(Initiation, [Omar, Farah], required: 2, exclude: false);
        var request = await RequestAsync(Initiation, Musa);
        var before = (await NotifiedAsync()).Count;

        await DecideAsync(Omar, Initiation, request.Gate.CurrentRequest!.Number, GateDecisionKind.Approve);

        Assert.Equal(before, (await NotifiedAsync()).Count);
    }
}
