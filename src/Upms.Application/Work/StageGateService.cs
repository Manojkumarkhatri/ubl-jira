using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The stages and gates of Stage-gate projects (Phase 3 FR-031–FR-040; research R13–R17). Every change takes the
/// project's planning lock and reads the gate after it, so two approvers deciding at once are both counted. Each gate
/// event is recorded on the stage's history and in the audit log.</summary>
internal sealed class StageGateService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IProjectTeam team,
    IUserDirectory users,
    IPlanLock planLock,
    IPlanningAuditLog audit,
    IWorkItemNumberAllocator numbers,
    StageLocks locks,
    StageGateRules rules,
    AssigneeReads assignees,
    INotificationCenter notifications,
    WatchList watchers,
    SearchIndexer indexer,
    ICallerContext caller,
    TimeProvider time) : IStageGateService
{
    private const int MaxPeople = 20;

    public async Task<Result<StagesView>> GetAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await RequireAsync(projectKey, ProjectRight.View, ct);
        return allowed.IsSuccess ? await ViewAsync(allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<StagesView>> AddStageAsync(string projectKey, string title, string? afterStageKey, CancellationToken ct)
    {
        var allowed = await RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (WorkItem.ValidateTitle(title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(caller.ProjectId, ct);
        var order = await locks.OrderAsync(caller.ProjectId, ct);
        long? afterId = null;
        if (afterStageKey is not null)
        {
            afterId = order.Stages.FirstOrDefault(s => s.Key == afterStageKey)?.Id;
            if (afterId is null)
            {
                return AppError.NotFound("stage");
            }
        }

        if (order.CheckAddAfter(afterId, out var before, out var after) is { } fixedOrder)
        {
            return fixedOrder.ToAppError();
        }

        var start = await workflow.FirstToDoStatusAsync(caller.ProjectId, ct);
        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        var stage = WorkItem.Create(caller.ProjectId, caller.Key, await numbers.NextAsync(caller.ProjectId, ct), WorkItemType.Stage, title,
            new StatusRef(start.Id, start.Name, start.Category), Rank.Between(before, after), ProjectTemplate.StageGate, context).Value!;
        db.WorkItems.Add(stage);
        await db.SaveChangesAsync(ct);
        rules.AddGateFor(stage);
        await watchers.AddAsync(stage.Id, [caller.UserId], ct); // the creator watches (Phase 4 FR-011)
        await indexer.IndexAsync(stage, ct); // found by search at once (Phase 4 research R15)
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ViewAsync(caller, ct);
    }

    public async Task<Result<StagesView>> MoveStageAsync(string stageKey, int offset, byte[] expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadStageAsync(stageKey, ProjectRight.Manage, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (stage, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(stage.ProjectId, ct);
        await db.Entry(stage).ReloadAsync(ct);
        if (!stage.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return AppError.Conflict($"{stage.Title} was changed by someone else. The current stages are shown.", await ViewAsync(caller, ct));
        }

        var order = await locks.OrderAsync(stage.ProjectId, ct);
        if (order.CheckMove(stage.Id, offset, out var before, out var after) is { } fixedOrder)
        {
            return fixedOrder.ToAppError();
        }

        // Recorded like a card move, with the old and new positions (research R13).
        var position = order.Stages.ToList().FindIndex(s => s.Id == stage.Id) + 1;
        var status = (await workflow.StatusesAsync(stage.ProjectId, ct)).Single(s => s.Id == stage.StatusId);
        var statusRef = new StatusRef(status.Id, status.Name, status.Category);
        stage.Move(statusRef, statusRef, Rank.Between(before, after), position, position + offset, offset < 0 ? "moved up" : "moved down",
            ChangeContext.New(caller.UserId, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ViewAsync(caller, ct);
    }

    public async Task<Result<IReadOnlyList<ApproverOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct)
    {
        var allowed = await RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (string.IsNullOrWhiteSpace(term))
        {
            return Result<IReadOnlyList<ApproverOption>>.Ok([]);
        }

        var members = (await team.GetMembersAsync(allowed.Value!.ProjectId, ct)).Select(m => m.UserId).ToHashSet();
        var found = await users.SearchActiveAsync(term.Trim(), MaxPeople, ct);
        return found.Select(u => new ApproverOption(u.Id, u.DisplayName, u.UserName, members.Contains(u.Id))).ToList();
    }

    public async Task<Result<StageView>> SetUpGateAsync(string stageKey, IReadOnlyList<Guid> approverIds, int? requiredApprovals,
        bool excludeRequester, byte[] expectedGateVersion, CancellationToken ct)
    {
        return await ChangeGateAsync(stageKey, ProjectRight.Manage, expectedGateVersion, async (stage, gate, caller, context) =>
        {
            var before = await DescribeSetupAsync(gate, ct);
            if (gate.SetUp(approverIds, requiredApprovals, excludeRequester, context) is { } refused)
            {
                return refused.ToAppError();
            }

            // Approvers can come from anywhere in the organization; people outside the project become Viewers (R16).
            var people = await users.GetAsync(approverIds.Distinct().ToList(), ct);
            foreach (var id in approverIds.Distinct())
            {
                if (!people.ContainsKey(id))
                {
                    return AppError.Validation("Approvers", "Choose approvers from the people found.");
                }

                var added = await team.AddViewerAsync(stage.ProjectId, id, ct);
                if (!added.IsSuccess)
                {
                    return added.Error!;
                }
            }

            var after = await DescribeSetupAsync(gate, ct);
            stage.RecordActivity(WorkItemField.GateSetup, before, after, context);
            await audit.WriteAsync(PlanningAuditEvent.GateSetupChanged, stage.Key,
                new { Approvers = gate.Approvers.Select(a => a.UserId).ToList(), gate.RequiredApprovals, gate.ExcludeRequester }, ct);
            return null;
        }, ct);
    }

    public async Task<Result<RequestPreview>> PreviewRequestAsync(string stageKey, CancellationToken ct)
    {
        var loaded = await LoadStageAsync(stageKey, ProjectRight.Contribute, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var stage = loaded.Value.Stage;
        var statuses = await workflow.StatusesAsync(stage.ProjectId, ct);
        var rows = await db.WorkItems.AsNoTracking()
            .Where(w => w.ContainerId == stage.Id && w.ResolvedAt == null)
            .OrderBy(w => w.Number)
            .Select(w => new { w.Key, w.Type, w.Title, w.StatusId, w.AssigneeId, w.DueDate, w.RowVersion })
            .ToListAsync(ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(stage.ProjectId, ct),
            rows.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        return new RequestPreview(stage.Key, rows.ConvertAll(r => new ContainedItemView(r.Key, r.Type, r.Title,
            WorkItemReads.ToOption(statuses, r.StatusId), r.AssigneeId is { } id ? people[id] : null, r.DueDate, r.RowVersion)));
    }

    public Task<Result<StageView>> RequestApprovalAsync(string stageKey, byte[] expectedGateVersion, CancellationToken ct) =>
        ChangeGateAsync(stageKey, ProjectRight.Contribute, expectedGateVersion, async (stage, gate, _, context) =>
        {
            var order = await locks.OrderAsync(stage.ProjectId, ct);
            var active = (await team.GetMembersAsync(stage.ProjectId, ct)).Where(m => m.IsActive).Select(m => m.UserId).ToHashSet();
            if (gate.Request(order.PlaceOf(stage.Id) == StagePlace.Current, active, context) is { } refused)
            {
                return refused.Code == ErrorCodes.StageLocked && order.Current is { } current
                    ? AppError.Rule(ErrorCodes.StageLocked, $"Only the current stage's gate can be requested. The {current.Title} gate must be approved first.")
                    : refused.ToAppError();
            }

            var request = gate.CurrentRequest!;
            stage.RecordActivity(WorkItemField.GateRequested, null, $"Request {request.Number}", context);
            await audit.WriteAsync(PlanningAuditEvent.GateApprovalRequested, stage.Key, new { Request = request.Number }, ct);

            // Each approver who may decide is told; an excluded requester is not (Phase 4 FR-016).
            var deciders = gate.Approvers.Select(a => a.UserId).Where(id => gate.CanDecide(id, request)).ToList();
            await notifications.RaiseAsync(
                [new NotificationEvent(NotificationKind.ApprovalRequested, stage.ProjectId, stage.Id, context.ActorId, deciders)], ct);
            return null;
        }, ct);

    public Task<Result<StageView>> WithdrawAsync(string stageKey, int requestNumber, CancellationToken ct) =>
        ChangeGateAsync(stageKey, ProjectRight.Contribute, null, async (stage, gate, _, context) =>
        {
            if (gate.Withdraw(requestNumber, context) is { } refused)
            {
                return refused.ToAppError();
            }

            stage.RecordActivity(WorkItemField.GateWithdrawn, $"Request {requestNumber}", null, context);
            await audit.WriteAsync(PlanningAuditEvent.GateRequestWithdrawn, stage.Key, new { Request = requestNumber }, ct);
            return null;
        }, ct);

    public Task<Result<StageView>> DecideAsync(string stageKey, int requestNumber, GateDecisionKind decision, string? note,
        CancellationToken ct) =>
        ChangeGateAsync(stageKey, ProjectRight.View, null, async (stage, gate, _, context) =>
        {
            if (await caller.GetAsync(ct) is not { } me || !me.Can(Rights.ApproveGates))
            {
                // The "Approve stage gates" tick (owner's decision of 2026-10-20).
                return AppError.Forbidden("Your ticks do not include approving stage gates. Ask an Admin or your Head.");
            }

            if (gate.Decide(requestNumber, context.ActorId, decision, note, context) is { } refused)
            {
                return refused.Code == StageGate.NotAnApproverCode ? AppError.Forbidden(refused.Message) : refused.ToAppError();
            }

            var recorded = gate.Requests.Single(r => r.Number == requestNumber).Decisions.Single(d => d.ApproverId == context.ActorId);
            stage.RecordActivity(WorkItemField.GateDecision, $"Request {requestNumber}",
                decision == GateDecisionKind.Approve ? "Approved" : "Sent back", context, recorded.Note);
            await audit.WriteAsync(PlanningAuditEvent.GateDecisionRecorded, stage.Key,
                new { Request = requestNumber, Decision = decision.ToString(), recorded.Note }, ct);
            if (gate.IsApproved)
            {
                stage.RecordActivity(WorkItemField.GateApproved, null, "Approved", context);
                await audit.WriteAsync(PlanningAuditEvent.GateApproved, stage.Key, new { Request = requestNumber }, ct);
            }

            // The requester hears the outcome once the gate is approved or sent back, not about each approval on the way.
            var outcome = decision == GateDecisionKind.SendBack ? "Sent back" : gate.IsApproved ? "Approved" : null;
            if (outcome is not null)
            {
                var requester = gate.Requests.Single(r => r.Number == requestNumber).RequestedById;
                await notifications.RaiseAsync(
                    [new NotificationEvent(NotificationKind.GateDecided, stage.ProjectId, stage.Id, context.ActorId, [requester], outcome)], ct);
            }

            return null;
        }, ct);

    /// <summary>Loads the stage and its gate under the planning lock, checks the version the caller saw, applies the
    /// change, saves it with its history and audit events, and answers with the stage as it is now. A request that ended
    /// meanwhile is a conflict that shows the current gate.</summary>
    private async Task<Result<StageView>> ChangeGateAsync(string stageKey, ProjectRight right, byte[]? expectedGateVersion,
        Func<WorkItem, StageGate, ProjectAccessInfo, ChangeContext, Task<AppError?>> change, CancellationToken ct)
    {
        var loaded = await LoadStageAsync(stageKey, right, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (stage, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(stage.ProjectId, ct);
        var gate = await db.StageGates
            .Include(g => g.Approvers)
            .Include(g => g.Requests).ThenInclude(r => r.Decisions)
            .AsSplitQuery()
            .SingleAsync(g => g.StageId == stage.Id, ct);
        if (expectedGateVersion is not null && !gate.RowVersion.AsSpan().SequenceEqual(expectedGateVersion))
        {
            return AppError.Conflict($"The {stage.Title} gate was changed by someone else. Its current state is shown.",
                await StageViewAsync(stage, caller, ct));
        }

        var error = await change(stage, gate, caller, ChangeContext.New(caller.UserId, time.GetUtcNow()));
        if (error is not null)
        {
            await transaction.RollbackAsync(ct);
            return error.Code == StageGate.RequestEndedCode
                ? AppError.Conflict(error.Message, await StageViewAsync(stage, caller, ct))
                : error;
        }

        await db.SaveChangesAsync(ct);
        if (gate.CurrentRequestId != gate.LatestRequest?.Id)
        {
            gate.TrackLatestRequest();
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return await StageViewAsync(stage, caller, ct);
    }

    private async Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, right, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        return allowed.Value!.Template == ProjectTemplate.StageGate ? allowed : AppError.NotFound("stages");
    }

    /// <summary>A live stage of a Stage-gate project, tracked, and the caller with the right.</summary>
    private async Task<Result<(WorkItem Stage, ProjectAccessInfo Caller)>> LoadStageAsync(string stageKey, ProjectRight right,
        CancellationToken ct)
    {
        var stage = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == stageKey && w.Type == WorkItemType.Stage, ct);
        if (stage is null)
        {
            return AppError.NotFound("stage");
        }

        var allowed = await access.RequireAsync(stage.ProjectId, right, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        return (stage, allowed.Value!);
    }

    private async Task<StageView> StageViewAsync(WorkItem stage, ProjectAccessInfo caller, CancellationToken ct) =>
        (await ViewAsync(caller, ct)).Stages.Single(s => s.Key == stage.Key);

    private async Task<StagesView> ViewAsync(ProjectAccessInfo caller, CancellationToken ct)
    {
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var order = await locks.OrderAsync(caller.ProjectId, ct);
        var ids = order.Stages.Select(s => s.Id).ToList();
        var stages = await db.WorkItems.AsNoTracking().Where(w => ids.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var gates = await db.StageGates.AsNoTracking()
            .Include(g => g.Approvers)
            .Include(g => g.Requests).ThenInclude(r => r.Decisions)
            .AsSplitQuery()
            .Where(g => ids.Contains(g.StageId))
            .ToDictionaryAsync(g => g.StageId, ct);
        var progress = await db.WorkItems.AsNoTracking()
            .Where(w => w.ContainerId != null && ids.Contains(w.ContainerId.Value) && w.Type != WorkItemType.Milestone)
            .GroupBy(w => w.ContainerId!.Value)
            .Select(g => new { StageId = g.Key, Total = g.Count(), Done = g.Count(w => w.ResolvedAt != null) })
            .ToDictionaryAsync(g => g.StageId, ct);
        var members = (await team.GetMembersAsync(caller.ProjectId, ct)).ToDictionary(m => m.UserId);
        var people = await users.GetAsync(gates.Values
            .SelectMany(g => g.Approvers.Select(a => a.UserId)
                .Concat(g.Requests.Select(r => r.RequestedById))
                .Concat(g.Requests.SelectMany(r => r.Decisions.Select(d => d.ApproverId))))
            .Distinct().ToList(), ct);

        var views = order.Stages.Select(slot =>
        {
            var stage = stages[slot.Id];
            var place = order.PlaceOf(slot.Id);
            var counts = progress.GetValueOrDefault(slot.Id);
            var gate = gates.GetValueOrDefault(slot.Id);
            return new StageView(stage.Key, stage.Title, place, stage.StartDate, stage.DueDate,
                new Progress(counts?.Done ?? 0, counts?.Total ?? 0, stage.ResolvedAt is not null),
                gate is null ? EmptyGate : GateViewOf(gate, members, people, caller.UserId), stage.RowVersion,
                place == StagePlace.Locked ? order.Current?.Title : null);
        }).ToList();
        return new StagesView(info.Key, info.Name, caller.CanContribute, caller.CanManage, caller.IsAdministrator, views);
    }

    private static readonly GateView EmptyGate = new(GateState.NotRequested, [], 0, true, true, null, false, []);

    private static GateView GateViewOf(StageGate gate, IReadOnlyDictionary<Guid, TeamMemberInfo> members,
        IReadOnlyDictionary<Guid, UserDisplay> people, Guid viewerId)
    {
        string Name(Guid id) => WorkItemReads.NameOf(people, id);
        var shown = gate.State == GateState.NotRequested ? null : gate.LatestRequest;
        var request = shown is null
            ? null
            : new GateRequestView(shown.Number, Name(shown.RequestedById), shown.RequestedById, shown.RequestedAt,
                shown.Decisions.OrderBy(d => d.DecidedAt).Select(d => new GateDecisionView(d.ApproverId, Name(d.ApproverId), d.Decision, d.Note, d.DecidedAt)).ToList(),
                gate.SentBackNote);
        var pending = gate.CurrentRequest;
        var canDecide = pending is not null && gate.CanDecide(viewerId, pending) && pending.Decisions.All(d => d.ApproverId != viewerId);
        return new GateView(gate.State,
            gate.Approvers.Select(a => new ApproverView(a.UserId, Name(a.UserId), members.GetValueOrDefault(a.UserId)?.IsActive == true))
                .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
            gate.ApprovalsNeeded, gate.RequiredApprovals is null, gate.ExcludeRequester, request, canDecide, gate.RowVersion);
    }

    /// <summary>"Approvers: Farah Khan, Omar Ali · approvals needed: all · the requester can't approve", for the history.</summary>
    private async Task<string?> DescribeSetupAsync(StageGate gate, CancellationToken ct)
    {
        if (gate.Approvers.Count == 0)
        {
            return null;
        }

        var people = await users.GetAsync(gate.Approvers.Select(a => a.UserId).ToList(), ct);
        var names = string.Join(", ", gate.Approvers.Select(a => WorkItemReads.NameOf(people, a.UserId)).Order(StringComparer.CurrentCultureIgnoreCase));
        return $"Approvers: {names} · approvals needed: {(gate.RequiredApprovals?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all")} · "
            + (gate.ExcludeRequester ? "the requester can't approve" : "the requester can approve");
    }
}
