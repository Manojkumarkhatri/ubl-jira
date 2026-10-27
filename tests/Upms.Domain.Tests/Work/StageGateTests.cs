using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>A stage's gate: setup, requests and decisions (Phase 3 FR-035–FR-038; data-model.md "Gate rules").</summary>
public sealed class StageGateTests
{
    private static readonly Guid Sara = Guid.NewGuid();    // Project Admin
    private static readonly Guid Amina = Guid.NewGuid();   // Member, requests approval
    private static readonly Guid Farah = Guid.NewGuid();   // the sponsor, a Viewer
    private static readonly Guid Omar = Guid.NewGuid();    // Member
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<Guid> Active = [Sara, Amina, Farah, Omar];

    private static ChangeContext By(Guid actor, int minutes = 0) => ChangeContext.New(actor, T0.AddMinutes(minutes));

    private static StageGate Gate(int? required = null, bool exclude = true, params Guid[] approvers)
    {
        var gate = StageGate.Create(1, 10);
        Assert.Null(gate.SetUp(approvers.Length == 0 ? [Farah, Omar] : approvers, required, exclude, By(Sara)));
        return gate;
    }

    private static StageGate Requested(int? required = null, bool exclude = true, Guid? requester = null, params Guid[] approvers)
    {
        var gate = Gate(required, exclude, approvers);
        Assert.Null(gate.Request(isCurrentStage: true, Active, By(requester ?? Amina, 1)));
        return gate;
    }

    // ----- Setup -----

    [Fact]
    public void A_new_gate_is_not_requested_needs_all_approvers_and_excludes_the_requester()
    {
        var gate = StageGate.Create(1, 10);

        Assert.Equal((GateState.NotRequested, (int?)null, true), (gate.State, gate.RequiredApprovals, gate.ExcludeRequester));
        Assert.Empty(gate.Approvers);
    }

    [Fact]
    public void P3_US3_AS1_Approvers_and_the_number_needed_are_set_up()
    {
        var gate = Gate(required: 1, exclude: false);

        Assert.Equal(new[] { Farah, Omar }.Order(), gate.Approvers.Select(a => a.UserId).Order());
        Assert.All(gate.Approvers, a => Assert.Equal((Sara, T0), (a.AddedById, a.AddedAt)));
        Assert.Equal((1, false, 1), (gate.RequiredApprovals, gate.ExcludeRequester, gate.ApprovalsNeeded));
        Assert.Equal(2, Gate().ApprovalsNeeded); // null means all approvers
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void P3_FR035_The_number_needed_is_from_one_to_the_number_of_approvers(int required)
    {
        var gate = StageGate.Create(1, 10);

        Assert.Equal("RequiredApprovals", gate.SetUp([Farah, Omar], required, true, By(Sara))?.Field);
    }

    [Fact]
    public void A_gate_needs_at_least_one_approver()
    {
        Assert.Equal("Approvers", StageGate.Create(1, 10).SetUp([], null, true, By(Sara))?.Field);
    }

    [Fact]
    public void P3_US3_AS8_Setup_waits_until_a_pending_request_is_withdrawn()
    {
        var gate = Requested();

        Assert.Equal(StageGate.RequestPendingCode, gate.SetUp([Farah], null, true, By(Sara, 2))?.Code);
        Assert.Null(gate.Withdraw(1, By(Amina, 3)));
        Assert.Null(gate.SetUp([Farah], null, true, By(Sara, 4)));
        Assert.Equal(GateState.NotRequested, gate.State);
    }

    // ----- Requests -----

    [Fact]
    public void P3_US3_AS3_A_request_awaits_a_decision()
    {
        var gate = Requested();

        Assert.Equal(GateState.AwaitingDecision, gate.State);
        var request = gate.CurrentRequest!;
        Assert.Equal((1, Amina, GateOutcome.Pending), (request.Number, request.RequestedById, request.Outcome));
    }

    [Fact]
    public void P3_FR036_Only_the_current_stage_s_gate_can_be_requested()
    {
        Assert.Equal(StageGate.StageLockedCode, Gate().Request(isCurrentStage: false, Active, By(Amina))?.Code);
    }

    [Fact]
    public void P3_FR036_A_request_needs_approvers_who_can_all_still_decide()
    {
        Assert.Equal(StageGate.NoApproversCode, StageGate.Create(1, 10).Request(true, Active, By(Amina))?.Code);
        Assert.Equal(StageGate.ApproverCannotDecideCode, Gate().Request(true, new HashSet<Guid> { Amina, Omar }, By(Amina))?.Code);
    }

    [Fact]
    public void P3_US3_AS11_An_excluded_requester_needs_enough_other_approvers()
    {
        var gate = Gate();

        Assert.Equal(StageGate.NotEnoughApproversCode, gate.Request(true, Active, By(Omar))?.Code);
        Assert.Null(gate.SetUp([Farah, Omar], 1, true, By(Sara, 1)));
        Assert.Null(gate.Request(true, Active, By(Omar, 2)));
        Assert.Equal(StageGate.NotAnApproverCode, gate.Decide(1, Omar, GateDecisionKind.Approve, null, By(Omar, 3))?.Code);
        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 4)));
        Assert.Equal(GateState.Approved, gate.State);
    }

    [Fact]
    public void A_requester_who_is_not_excluded_may_approve()
    {
        var gate = Requested(exclude: false, requester: Omar);

        Assert.Null(gate.Decide(1, Omar, GateDecisionKind.Approve, null, By(Omar, 2)));
    }

    [Fact]
    public void A_second_request_while_one_is_pending_is_refused()
    {
        Assert.Equal(StageGate.RequestPendingCode, Requested().Request(true, Active, By(Amina, 2))?.Code);
    }

    // ----- Decisions -----

    [Fact]
    public void P3_US3_AS4_The_gate_is_approved_when_the_approvals_reach_the_number_needed()
    {
        var gate = Requested();

        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, "Good plan.", By(Farah, 2)));
        Assert.Equal(GateState.AwaitingDecision, gate.State);
        Assert.Null(gate.Decide(1, Omar, GateDecisionKind.Approve, null, By(Omar, 3)));

        Assert.Equal((GateState.Approved, (DateTimeOffset?)T0.AddMinutes(3)), (gate.State, gate.ApprovedAt));
        var request = gate.Requests.Single();
        Assert.Equal((GateOutcome.Approved, (Guid?)Omar), (request.Outcome, request.ClosedById));
        Assert.Equal(["Good plan.", null], request.Decisions.Select(d => d.Note));
    }

    [Fact]
    public void P3_US3_AS4_One_approval_is_enough_when_one_is_needed()
    {
        var gate = Requested(required: 1);

        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 2)));

        Assert.Equal(GateState.Approved, gate.State);
    }

    [Fact]
    public void P3_US3_AS5_Sending_back_needs_a_note_and_ends_the_request()
    {
        var gate = Requested();
        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 2)));

        Assert.Equal("Note", gate.Decide(1, Omar, GateDecisionKind.SendBack, "  ", By(Omar, 3))?.Field);
        Assert.Null(gate.Decide(1, Omar, GateDecisionKind.SendBack, "The budget is missing.", By(Omar, 4)));

        Assert.Equal((GateState.SentBack, GateOutcome.SentBack), (gate.State, gate.Requests.Single().Outcome));
        Assert.Equal("The budget is missing.", gate.SentBackNote);
        Assert.Null(gate.Request(true, Active, By(Amina, 5)));
        Assert.Equal(2, gate.CurrentRequest!.Number);
        Assert.Null(gate.SentBackNote);
    }

    [Fact]
    public void Notes_have_at_most_1000_characters()
    {
        Assert.Equal("Note", Requested().Decide(1, Farah, GateDecisionKind.Approve, new string('n', 1001), By(Farah, 2))?.Field);
    }

    [Fact]
    public void P3_US3_AS6_Only_approvers_decide_and_each_once()
    {
        var gate = Requested();

        Assert.Equal(StageGate.NotAnApproverCode, gate.Decide(1, Sara, GateDecisionKind.Approve, null, By(Sara, 2))?.Code);
        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 3)));
        Assert.Equal(StageGate.AlreadyDecidedCode, gate.Decide(1, Farah, GateDecisionKind.SendBack, "No.", By(Farah, 4))?.Code);
    }

    [Fact]
    public void A_decision_on_a_request_that_ended_is_refused()
    {
        var gate = Requested();
        Assert.Null(gate.Withdraw(1, By(Amina, 2)));

        Assert.Equal(StageGate.RequestEndedCode, gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 3))?.Code);
        Assert.Equal(StageGate.RequestEndedCode, gate.Withdraw(1, By(Amina, 4))?.Code);
    }

    [Fact]
    public void P3_US3_AS8_A_withdrawal_clears_the_request_s_decisions_from_the_count()
    {
        var gate = Requested();
        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 2)));
        Assert.Null(gate.Withdraw(1, By(Amina, 3)));
        Assert.Null(gate.Request(true, Active, By(Amina, 4)));

        Assert.Null(gate.Decide(2, Omar, GateDecisionKind.Approve, null, By(Omar, 5)));

        Assert.Equal(GateState.AwaitingDecision, gate.State); // farah's approval belonged to the withdrawn request
        Assert.Equal(GateOutcome.Withdrawn, gate.Requests.Single(r => r.Number == 1).Outcome);
        Assert.Single(gate.Requests.Single(r => r.Number == 1).Decisions);
    }

    [Fact]
    public void P3_US3_AS9_An_approved_gate_never_changes()
    {
        var gate = Requested(required: 1);
        Assert.Null(gate.Decide(1, Farah, GateDecisionKind.Approve, null, By(Farah, 2)));

        Assert.Equal(StageGate.ApprovedCode, gate.Withdraw(1, By(Amina, 3))?.Code);
        Assert.Equal(StageGate.ApprovedCode, gate.SetUp([Omar], null, true, By(Sara, 4))?.Code);
        Assert.Equal(StageGate.ApprovedCode, gate.Request(true, Active, By(Amina, 5))?.Code);
        Assert.Equal(StageGate.RequestEndedCode, gate.Decide(1, Omar, GateDecisionKind.SendBack, "Late.", By(Omar, 6))?.Code);
    }

    [Fact]
    public void Deleting_the_stage_cancels_a_pending_request_and_restoring_it_starts_over()
    {
        var gate = Requested();

        gate.Cancel(By(Sara, 2));

        Assert.Equal((GateState.NotRequested, GateOutcome.Cancelled), (gate.State, gate.Requests.Single().Outcome));
        Assert.Null(gate.CurrentRequest);
    }
}
