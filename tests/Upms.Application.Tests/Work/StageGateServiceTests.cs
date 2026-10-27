using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Stages and their gates (Phase 3 FR-031, FR-035–FR-041). amina is the Project Admin; musa requests approval.</summary>
public sealed class StageGateServiceTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    private Task<Result<IReadOnlyList<WaitingGateRow>>> GatesWaitingAsync() =>
        CallAsync<IMyTasksService, Result<IReadOnlyList<WaitingGateRow>>>(s => s.ListGatesAsync(Ct));

    [Fact]
    public async Task P3_US3_AS1_Naming_approvers_adds_people_from_outside_as_Viewers()
    {
        var view = await SetUpAsync(Initiation, [Farah, Omar]);

        Assert.Equal(["Farah Tester", "Omar Tester"], view.Gate.Approvers.Select(a => a.DisplayName).Order());
        Assert.Equal((2, true, true), (view.Gate.ApprovalsNeeded, view.Gate.RequiredIsAll, view.Gate.ExcludeRequester));
        var farahRole = await QueryAsync(db => db.ProjectMembers.Where(m => m.UserId == Farah.Id).Select(m => (ProjectRole?)m.Role).SingleAsync(Ct));
        Assert.Equal(ProjectRole.Viewer, farahRole);
        var audit = await AuditEventsAsync();
        Assert.Contains(audit, e => e.EventType == AuditEventType.MemberAdded && e.SubjectUserId == Farah.Id);
        Assert.Contains(audit, e => e.EventType == AuditEventType.GateSetupChanged && e.Target == Initiation);
        Assert.Equal([StagePlace.Current, StagePlace.Locked, StagePlace.Locked, StagePlace.Locked], (await StagesAsync()).Stages.Select(s => s.Place));
        Assert.Equal("Initiation", (await StageAsync(Planning)).WaitsFor);
    }

    [Fact]
    public async Task People_are_found_across_the_organization()
    {
        var found = (await CallAsync<IStageGateService, Result<IReadOnlyList<ApproverOption>>>(s => s.FindPeopleAsync("WEB", "far", Ct))).ValueOrThrow();

        var farah = Assert.Single(found);
        Assert.Equal((Farah.Id, false), (farah.UserId, farah.IsMember));
    }

    [Fact]
    public async Task P3_US3_AS3_A_request_shows_open_items_first_and_then_waits_for_each_approver()
    {
        var open = await TaskInAsync(Initiation, "Write the charter");
        await SetUpAsync(Initiation, [Farah, Omar], then: Musa);

        var preview = (await CallAsync<IStageGateService, Result<RequestPreview>>(s => s.PreviewRequestAsync(Initiation, Ct))).ValueOrThrow();
        Assert.Equal([open], preview.OpenItems.Select(i => i.Key));
        var requested = await RequestAsync(Initiation, Musa);

        Assert.Equal(GateState.AwaitingDecision, requested.Gate.State);
        Assert.Equal(("Musa Tester", 1), (requested.Gate.CurrentRequest!.RequestedBy, requested.Gate.CurrentRequest.Number));
        foreach (var approver in new[] { Farah, Omar })
        {
            ActAs(approver);
            var waiting = Assert.Single((await GatesWaitingAsync()).ValueOrThrow());
            Assert.Equal(("WEB", Initiation, "Initiation", "Musa Tester"), (waiting.ProjectKey, waiting.StageKey, waiting.StageTitle, waiting.RequestedBy));
            Assert.True((await StageAsync(Initiation)).Gate.CanDecide);
        }

        ActAs(Musa);
        Assert.Empty((await GatesWaitingAsync()).ValueOrThrow());
    }

    [Fact]
    public async Task P3_US3_AS4_The_gate_opens_the_next_stage_when_both_approve()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);

        var afterFarah = await DecideAsync(Farah, Initiation, 1, GateDecisionKind.Approve, "Charter looks good.");
        Assert.Equal(GateState.AwaitingDecision, afterFarah.Gate.State);
        ActAs(Farah);
        Assert.Empty((await GatesWaitingAsync()).ValueOrThrow());
        ActAs(Owner);

        var approved = await DecideAsync(Omar, Initiation, 1, GateDecisionKind.Approve);

        Assert.Equal(GateState.Approved, approved.Gate.State);
        Assert.Equal([StagePlace.Approved, StagePlace.Current, StagePlace.Locked, StagePlace.Locked], (await StagesAsync()).Stages.Select(s => s.Place));
    }

    [Fact]
    public async Task P3_US3_AS4_One_approval_is_enough_when_one_is_needed()
    {
        await SetUpAsync(Initiation, [Farah, Omar], required: 1);
        await RequestAsync(Initiation, Musa);

        Assert.Equal(GateState.Approved, (await DecideAsync(Farah, Initiation, 1, GateDecisionKind.Approve)).Gate.State);
    }

    [Fact]
    public async Task P3_US3_AS5_Sending_back_needs_a_note_and_approval_can_be_requested_again()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);
        await DecideAsync(Farah, Initiation, 1, GateDecisionKind.Approve);

        ActAs(Omar);
        Assert.True((await TryDecideAsync(Initiation, 1, GateDecisionKind.SendBack)).Error!.FieldErrors!.ContainsKey("Note"));
        var sentBack = (await TryDecideAsync(Initiation, 1, GateDecisionKind.SendBack, "The budget is missing.")).ValueOrThrow();

        Assert.Equal(GateState.SentBack, sentBack.Gate.State);
        Assert.Equal("The budget is missing.", sentBack.Gate.CurrentRequest!.SentBackNote);
        Assert.Equal(2, (await RequestAsync(Initiation, Musa)).Gate.CurrentRequest!.Number);
    }

    [Fact]
    public async Task P3_US3_AS6_Project_Admins_and_administrators_who_are_not_approvers_cannot_decide()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);

        Assert.Equal(ErrorKind.Forbidden, (await TryDecideAsync(Initiation, 1, GateDecisionKind.Approve)).Error!.Kind);
        ActAs(await Data.AdministratorAsync());
        Assert.Equal(ErrorKind.Forbidden, (await TryDecideAsync(Initiation, 1, GateDecisionKind.Approve)).Error!.Kind);
        Assert.False((await StageAsync(Initiation)).Gate.CanDecide);
    }

    [Fact]
    public async Task P3_US3_AS8_Approvers_change_only_after_the_request_is_withdrawn_and_its_decisions_stay_in_the_history()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);
        await DecideAsync(Farah, Initiation, 1, GateDecisionKind.Approve, "Fine by me.");

        Assert.Equal(ErrorCodes.GateRequestPending, (await TrySetUpAsync(Initiation, [Omar])).Error!.Code);
        ActAs(Musa);
        var withdrawn = (await TryWithdrawAsync(Initiation, 1)).ValueOrThrow();
        Assert.Equal(GateState.NotRequested, withdrawn.Gate.State);

        ActAs(Owner);
        Assert.Equal(["Omar Tester"], (await TrySetUpAsync(Initiation, [Omar])).ValueOrThrow().Gate.Approvers.Select(a => a.DisplayName));
        Assert.Contains((await HistoryAsync(Initiation)).Items, h => h.Field == WorkItemField.GateDecision && h.Note == "Fine by me.");
    }

    [Fact]
    public async Task P3_US3_AS9_An_approved_gate_can_neither_be_withdrawn_nor_changed()
    {
        await SetUpAsync(Initiation, [Omar], exclude: false);
        await RequestAsync(Initiation, Musa);
        await DecideAsync(Omar, Initiation, 1, GateDecisionKind.Approve);

        Assert.Equal(ErrorCodes.GateApproved, (await TryWithdrawAsync(Initiation, 1)).Error!.Code);
        Assert.Equal(ErrorCodes.GateApproved, (await TrySetUpAsync(Initiation, [Farah])).Error!.Code);
    }

    [Fact]
    public async Task P3_US3_AS10_Every_step_is_in_the_stage_s_history_and_the_audit_log()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);
        await DecideAsync(Omar, Initiation, 1, GateDecisionKind.SendBack, "The budget is missing.");
        await RequestAsync(Initiation, Musa);
        ActAs(Musa);
        (await TryWithdrawAsync(Initiation, 2)).ValueOrThrow();
        await RequestAsync(Initiation, Musa);
        await DecideAsync(Farah, Initiation, 3, GateDecisionKind.Approve);
        await DecideAsync(Omar, Initiation, 3, GateDecisionKind.Approve);

        var history = (await HistoryAsync(Initiation)).Items.Where(h => h.Field >= WorkItemField.GateSetup).ToList();
        Assert.Equal(
        [
            WorkItemField.GateSetup, WorkItemField.GateRequested, WorkItemField.GateDecision, WorkItemField.GateRequested,
            WorkItemField.GateWithdrawn, WorkItemField.GateRequested, WorkItemField.GateDecision, WorkItemField.GateDecision,
            WorkItemField.GateApproved,
        ], history.Select(h => h.Field));
        Assert.Equal(["Omar Tester", "Farah Tester", "Omar Tester"], history.Where(h => h.Field == WorkItemField.GateDecision).Select(h => h.ActorName));
        Assert.Equal("The budget is missing.", history.First(h => h.Field == WorkItemField.GateDecision).Note);

        var audit = (await AuditEventsAsync()).Where(e => e.Target == Initiation).Select(e => e.EventType).ToList();
        Assert.Equal(
        [
            AuditEventType.GateSetupChanged, AuditEventType.GateApprovalRequested, AuditEventType.GateDecisionRecorded,
            AuditEventType.GateApprovalRequested, AuditEventType.GateRequestWithdrawn, AuditEventType.GateApprovalRequested,
            AuditEventType.GateDecisionRecorded, AuditEventType.GateDecisionRecorded, AuditEventType.GateApproved,
        ], audit);
    }

    [Fact]
    public async Task P3_US3_AS11_The_requester_cannot_approve_their_own_request()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);

        ActAs(Omar);
        Assert.Equal(ErrorCodes.NotEnoughApprovers, (await TryRequestAsync(Initiation)).Error!.Code);
        await SetUpAsync(Initiation, [Farah, Omar], required: 1);
        await RequestAsync(Initiation, Omar);

        ActAs(Omar);
        Assert.Equal(ErrorKind.Forbidden, (await TryDecideAsync(Initiation, 1, GateDecisionKind.Approve)).Error!.Kind);
        Assert.Empty((await GatesWaitingAsync()).ValueOrThrow());
        Assert.Equal(GateState.Approved, (await DecideAsync(Farah, Initiation, 1, GateDecisionKind.Approve)).Gate.State);
    }

    [Fact]
    public async Task A_request_needs_approvers_who_can_all_still_decide()
    {
        ActAs(Musa);
        Assert.Equal(ErrorCodes.NoApprovers, (await TryRequestAsync(Initiation)).Error!.Code);

        await SetUpAsync(Initiation, [Farah, Omar]);
        await CallAsync<IProjectMemberService, Result<TeamView>>(async s =>
            await s.RemoveAsync("WEB", Farah.Id, (await s.GetTeamAsync("WEB", Ct)).Value!.MembersVersion, Ct));

        ActAs(Musa);
        Assert.Equal(ErrorCodes.ApproverCannotDecide, (await TryRequestAsync(Initiation)).Error!.Code);
        Assert.False((await StageAsync(Initiation)).Gate.Approvers.Single(a => a.UserId == Farah.Id).CanDecide);
    }

    [Fact]
    public async Task Only_the_current_stage_s_gate_can_be_requested()
    {
        await SetUpAsync(Planning, [Omar]);

        ActAs(Musa);
        Assert.Equal(ErrorCodes.StageLocked, (await TryRequestAsync(Planning)).Error!.Code);
    }

    [Fact]
    public async Task Two_approvers_deciding_at_the_same_moment_are_both_accepted()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);

        async Task<Result<StageView>> DecideAsUser(User user)
        {
            await Task.Yield();
            Harness.CurrentUser.UseInThisFlow(user.Id);
            return await CallAsync<IStageGateService, Result<StageView>>(s => s.DecideAsync(Initiation, 1, GateDecisionKind.Approve, null, Ct));
        }

        var results = await Task.WhenAll(DecideAsUser(Farah), DecideAsUser(Omar));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.Message));
        Assert.Equal(GateState.Approved, (await StageAsync(Initiation)).Gate.State);
    }

    [Fact]
    public async Task A_decision_on_a_withdrawn_request_returns_the_current_gate()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);
        ActAs(Musa);
        (await TryWithdrawAsync(Initiation, 1)).ValueOrThrow();

        ActAs(Farah);
        var result = await TryDecideAsync(Initiation, 1, GateDecisionKind.Approve);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(GateState.NotRequested, Assert.IsType<StageView>(result.Error.Current).Gate.State);
    }

    [Fact]
    public async Task P3_FR039_New_stages_go_after_the_current_one_and_locked_stages_move_among_themselves()
    {
        await ApproveAsync(Initiation);

        var added = (await CallAsync<IStageGateService, Result<StagesView>>(s => s.AddStageAsync("WEB", "Transition", null, Ct))).ValueOrThrow();
        Assert.Equal(["Initiation", "Planning", "Execution", "Closure", "Transition"], added.Stages.Select(s => s.Title));
        Assert.Equal(GateState.NotRequested, added.Stages[^1].Gate.State);
        Assert.Equal(ErrorCodes.StageOrderFixed, (await CallAsync<IStageGateService, Result<StagesView>>(s =>
            s.AddStageAsync("WEB", "Too early", Initiation, Ct))).Error!.Code);

        var closure = added.Stages.Single(s => s.Key == Closure);
        var moved = (await CallAsync<IStageGateService, Result<StagesView>>(s => s.MoveStageAsync(Closure, -1, closure.StageVersion, Ct))).ValueOrThrow();
        Assert.Equal(["Initiation", "Planning", "Closure", "Execution", "Transition"], moved.Stages.Select(s => s.Title));
        var planning = moved.Stages.Single(s => s.Key == Planning);
        Assert.Equal(ErrorCodes.StageOrderFixed, (await CallAsync<IStageGateService, Result<StagesView>>(s =>
            s.MoveStageAsync(Planning, 1, planning.StageVersion, Ct))).Error!.Code);
        Assert.Contains((await HistoryAsync(Closure)).Items, h => h.Field == WorkItemField.Rank);
    }

    [Fact]
    public async Task A_stage_from_the_list_s_create_box_gets_a_gate_and_goes_after_the_last_stage()
    {
        var stage = await CreateAsync(WorkItemType.Stage, "Handover");

        var last = (await StagesAsync()).Stages[^1];
        Assert.Equal((stage.Key, GateState.NotRequested, StagePlace.Locked), (last.Key, last.Gate.State, last.Place));
    }

    [Fact]
    public async Task Deleting_stages_keeps_approved_ones_and_cancels_a_pending_request()
    {
        await ApproveAsync(Initiation);
        Assert.Equal(ErrorCodes.GateApproved, (await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(Initiation, Ct))).Error!.Code);

        await SetUpAsync(Planning, [Farah]);
        await RequestAsync(Planning, Musa);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(Planning, Ct))).IsSuccess);

        ActAs(Farah);
        Assert.Empty((await GatesWaitingAsync()).ValueOrThrow());
        ActAs(Owner);
        Assert.Equal(Execution, (await StagesAsync()).Stages.Single(s => s.Place == StagePlace.Current).Key);
    }

    [Fact]
    public async Task A_restored_stage_goes_last_with_its_gate_not_requested_and_never_locks_started_work()
    {
        var planningTask = await TaskInAsync(Planning, "Plan the migration");
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(Initiation, Ct))).IsSuccess);
        (await StartAsync(planningTask)).ValueOrThrow();

        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(Initiation, Ct))).IsSuccess);
        ActAs(Owner);

        var stages = (await StagesAsync()).Stages;
        Assert.Equal([Planning, Execution, Closure, Initiation], stages.Select(s => s.Key));
        Assert.Equal((StagePlace.Current, GateState.NotRequested), (stages[0].Place, stages[^1].Gate.State));
    }

    [Fact]
    public async Task Members_cannot_manage_stages_or_gates_and_Viewers_cannot_request()
    {
        ActAs(Musa);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IStageGateService, Result<StagesView>>(s => s.AddStageAsync("WEB", "Extra", null, Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await TrySetUpAsync(Initiation, [Omar])).Error!.Kind);

        await SetUpAsync(Initiation, [Omar]);
        ActAs(Vera);
        Assert.Equal(ErrorKind.Forbidden, (await TryRequestAsync(Initiation)).Error!.Kind);
        Assert.False((await StagesAsync()).CanContribute);
    }

    [Fact]
    public async Task Kanban_and_Scrum_projects_have_no_stages()
    {
        await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Intranet", "INT", null, ProjectTemplate.Kanban, Ct));

        Assert.Equal(ErrorKind.NotFound, (await TryStagesAsync("INT")).Error!.Kind);
    }

    [Fact]
    public async Task P3_FR041_Gates_waiting_leave_My_tasks_when_the_approver_can_no_longer_see_the_project()
    {
        await SetUpAsync(Initiation, [Farah, Omar]);
        await RequestAsync(Initiation, Musa);
        await CallAsync<IProjectMemberService, Result<TeamView>>(async s =>
            await s.RemoveAsync("WEB", Omar.Id, (await s.GetTeamAsync("WEB", Ct)).Value!.MembersVersion, Ct));

        ActAs(Omar);
        Assert.Empty((await GatesWaitingAsync()).ValueOrThrow());
    }
}
