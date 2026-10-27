# Contract: Application Services (Phase 3)

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) |
**Permissions**: [permissions.md](./permissions.md)

This contract lists what Phase 3 adds to or changes in the Phase 1 and Phase 2 services
(`specs/002-kanban-project-core/contracts/application-services.md`,
`specs/003-project-views-and-team/contracts/application-services.md`); everything not mentioned stays as it
was (FR-057). The conventions are unchanged: the caller comes from `ICurrentUser`; every project-scoped
method checks `IProjectAccess` first; versioned writes return `Conflict` with the current state; lists are
paged. The rights named below (`View`, `Contribute`, `Manage`) are those of `IProjectAccess`. Signatures
are illustrative C#; the operations, permission rules and error codes are the contract.

**New rule violation codes**: `TypeNotAllowed`, `TypeChangeNotAllowed`, `InvalidContainer`, `NoSubtasks`,
`TemplateSwitchNotAllowed`, `SprintsOpen`, `SprintAlreadyActive`, `SprintCompleted`, `StageLocked`,
`GateNotApproved`, `GateApproved`, `GateRequestPending`, `NoApprovers`, `ApproverCannotDecide`,
`NotEnoughApprovers`, `AlreadyDecided`, `StageOrderFixed`, `DependencyToSelf`, `DuplicateDependency`,
`DependencyInHierarchy`, `DependencyLoop`.

## Common kernel

```csharp
public enum ProjectTemplate { Kanban, Scrum, StageGate }          // Upms.Domain.Common (research R2)
public enum WorkItemType { Task, Subtask, Epic, Story, Bug, Stage, Milestone }   // new values after Subtask
```

## Identity module

```csharp
public enum AuditEventType { /* Phase 1 and 2 values */ ProjectTemplateChanged, GateSetupChanged,
    GateApprovalRequested, GateRequestWithdrawn, GateDecisionRecorded, GateApproved }   // new values

public interface IPlanningAuditLog                                // contract for Projects and Work (research R17)
{
    // Writes the matching AuditEventType in the caller's transaction; target = project key or stage key.
    Task WriteAsync(PlanningAuditEvent change, string target, object details, CancellationToken ct);
}
public enum PlanningAuditEvent { TemplateChanged, GateSetupChanged, GateApprovalRequested, GateRequestWithdrawn,
    GateDecisionRecorded, GateApproved }
```

## Projects module

```csharp
// IProjectService (changed)
Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template,
    CancellationToken ct);   // any signed-in user; Stage-gate projects get their four stages (FR-001–FR-003)
Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target,
    int expectedDetailsVersion, CancellationToken ct);   // Manage; TemplateSwitchNotAllowed, SprintsOpen (FR-005)
// ProjectSummary and ProjectDetails gain ProjectTemplate Template (FR-006).

// IProjectAccess: ProjectAccessInfo gains ProjectTemplate Template.
// IProjectWorkflow: BoardInfo gains ProjectTemplate Template.
// IProjectMemberService: TeamView gains ProjectTemplate Template, for the project header (US1).

// IProjectTeam (changed, research R16)
Task<bool> AddViewerAsync(long projectId, Guid userId, CancellationToken ct);
    // Adds an active non-member as a Viewer in the caller's transaction (MemberAdded audit, MembersVersion + 1);
    // false when they are already a member; AccountDeactivated for an inactive account.
// TeamMemberInfo gains bool IsActive (an approver "can still decide" when they are an active member).
```

## Work module: contracts for the Projects module

```csharp
public interface IProjectPlanSetup                                // research R3
{
    Task CreateDefaultStagesAsync(long projectId, string projectKey, CancellationToken ct);  // four stages and gates
    Task<bool> HasOpenSprintsAsync(long projectId, CancellationToken ct);                    // active or planned
    Task PrepareForScrumAsync(long projectId, CancellationToken ct);  // project-wide card order (research R8)
}

// IWorkItemStatusMover (changed, research R15): the Projects module checks this before deleting a column whose
// items must go to a column that is not "to do"; a positive count refuses the deletion with StageLocked.
Task<int> CountInLockedStagesAsync(long statusId, CancellationToken ct);
```

## Work module: work items

```csharp
// IWorkItemService (changed)
Task<Result<WorkItemDetails>> CreateAsync(string projectKey, NewWorkItem item, CancellationToken ct);
    // Contribute (stages: Manage); from the list, the backlog and a container's drawer; starts in the leftmost
    // "to do" column; TypeNotAllowed, InvalidContainer, StageLocked (FR-010, FR-015, FR-021)
public sealed record NewWorkItem(WorkItemType Type, string Title, string? ContainerKey = null);
Task<Result<IReadOnlyList<ContainerOption>>> ListContainersAsync(string projectKey, CancellationToken ct);  // View
public sealed record ContainerOption(string Key, string Title, WorkItemType Type, bool IsLocked);

public abstract record WorkItemEdit
{
    // Phase 1 and 2: Title, Description, Priority, Status, Assignee, Dates
    public sealed record Type(WorkItemType Value) : WorkItemEdit;          // TypeChangeNotAllowed (FR-011)
    public sealed record Container(string? ContainerKey) : WorkItemEdit;   // InvalidContainer, StageLocked (FR-015, FR-033)
    public sealed record Estimate(decimal? Value) : WorkItemEdit;          // 0–999.9, one decimal (FR-014)
}
// Status and Dates edits also apply the Phase 3 rules: StageLocked, GateNotApproved; a milestone's Dates edit
// must have no start date (InvalidDates). Moving a container into "done" warns with its open items (FR-018).

// WorkItemDetails gains:
//   ProjectTemplate Template, IReadOnlyList<WorkItemType> TypeChoices (empty when the type cannot change),
//   ContainerRef? Container, decimal? Estimate, SprintRef? Sprint, bool IsLocked (and LockedBy: the gate to approve),
//   ContainerSummary? AsContainer (epics and stages: Progress, Page<ContainedItemView> Items),
//   DependencyList Dependencies, GateSummary? Gate (stages).
public sealed record ContainerRef(string Key, string Title, WorkItemType Type);
public sealed record SprintRef(long Id, string Name, SprintState State);
public sealed record Progress(int Done, int Total)               // FR-017
{
    public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Done / Total);
}
public sealed record ContainerSummary(Progress Progress, Page<ContainedItemView> Items);
public sealed record ContainedItemView(string Key, WorkItemType Type, string Title, StatusOption Status,
    AssigneeRef? Assignee, DateOnly? DueDate, byte[] Version);
// IWorkItemService.DeleteAsync: stages need Manage and are refused with GateApproved once approved; containers
// release their items; items leave planned and active sprints; dependencies are removed (FR-019, FR-052).
// AddSubtaskAsync: NoSubtasks for milestones, epics and stages.
```

## Work module: backlog and sprints (Scrum projects)

```csharp
public interface IBacklogService                                  // FR-021, FR-022, FR-024, FR-026
{
    Task<Result<BacklogView>> GetAsync(string projectKey, PageRequest backlogPage, CancellationToken ct);  // View
    // Contribute; Conflict with the current row when the card changed; SprintCompleted
    Task<Result<BacklogRow>> MoveAsync(string workItemKey, BacklogTarget target, BacklogPlacement placement,
        byte[] expectedVersion, CancellationToken ct);
}
public abstract record BacklogTarget
{
    public sealed record Backlog : BacklogTarget;
    public sealed record Sprint(long SprintId) : BacklogTarget;
}
// Where the card goes in the target list, as CardPlacement does on the board (Phase 1). Keep: the card keeps its
// rank and joins the target list where that rank falls ("Move to sprint" from the menu). Before: drag and drop.
public abstract record BacklogPlacement
{
    public sealed record Keep : BacklogPlacement;
    public sealed record Top : BacklogPlacement;
    public sealed record Bottom : BacklogPlacement;
    public sealed record Up : BacklogPlacement;
    public sealed record Down : BacklogPlacement;
    public sealed record Before(string WorkItemKey) : BacklogPlacement;
}
public sealed record BacklogView(string ProjectKey, string ProjectName, bool CanContribute, bool CanManageSprints,
    SprintSection? Active, IReadOnlyList<SprintSection> Planned, Page<BacklogRow> Backlog,
    IReadOnlyList<CompletedSprintLink> Completed);                 // Backlog: 100 per page, by rank
public sealed record SprintSection(long Id, int Number, string Name, string? Goal, SprintState State,
    DateOnly? StartDate, DateOnly? EndDate, SprintTotals Totals, IReadOnlyList<BacklogRow> Items, byte[] Version);
public sealed record SprintTotals(int Items, decimal Estimate, int Unestimated, decimal CompletedEstimate,
    decimal RemainingEstimate);                                    // completed/remaining for the active sprint
public sealed record BacklogRow(string Key, WorkItemType Type, string Title, ContainerRef? Epic, AssigneeRef? Assignee,
    decimal? Estimate, StatusOption Status, int SubtaskCount, byte[] Version);
public sealed record CompletedSprintLink(int Number, string Name, DateOnly? EndDate);

public interface ISprintService                                   // FR-023, FR-025, FR-028, FR-029
{
    Task<Result<SprintSection>> CreateAsync(string projectKey, CancellationToken ct);                   // Manage
    Task<Result<SprintSection>> UpdateAsync(long sprintId, string name, string? goal, byte[] expectedVersion,
        CancellationToken ct);                                    // Manage; SprintCompleted
    Task<Result<SprintSection>> StartAsync(long sprintId, DateOnly start, DateOnly? end, string? goal,
        byte[] expectedVersion, CancellationToken ct);            // Manage; SprintAlreadyActive, InvalidDates
    Task<Result<SprintSection>> CompleteAsync(long sprintId, long? moveOpenItemsToSprintId, byte[] expectedVersion,
        CancellationToken ct);                                    // Manage; null sends open cards to the backlog
    Task<Result> DeleteAsync(long sprintId, byte[] expectedVersion, CancellationToken ct);   // Manage; planned only
    Task<Result<SprintReport>> GetReportAsync(string projectKey, int sprintNumber, CancellationToken ct);   // View
}
public enum SprintState { Planned, Active, Completed }
public sealed record SprintReport(string ProjectKey, string Name, string? Goal, DateOnly StartDate, DateOnly EndDate,
    DateTimeOffset CompletedAt, ReportGroup Committed, ReportGroup Added, ReportGroup Removed, ReportGroup Completed,
    ReportGroup NotCompleted);
public sealed record ReportGroup(IReadOnlyList<ReportLine> Items, decimal EstimateTotal);
public sealed record ReportLine(string Key, WorkItemType Type, string Title, decimal? Estimate, bool IsDeleted);

// IBoardService (changed, FR-010, FR-027): BoardView gains ProjectTemplate Template, ActiveSprintInfo? Sprint
// (name, goal, dates; null with no active sprint on a Scrum board) and CanManageSprints; CardView gains
// WorkItemType Type and decimal? Estimate. CreateInlineAsync gains WorkItemType type (cards only); on a Scrum
// board the card joins the active sprint.
```

## Work module: stages and gates (Stage-gate projects)

```csharp
public interface IStageGateService                                // FR-031–FR-041
{
    Task<Result<StagesView>> GetAsync(string projectKey, CancellationToken ct);                         // View
    Task<Result<StagesView>> AddStageAsync(string projectKey, string title, string? afterStageKey,
        CancellationToken ct);                                    // Manage; StageOrderFixed
    Task<Result<StagesView>> MoveStageAsync(string stageKey, int offset, byte[] expectedVersion,
        CancellationToken ct);                                    // Manage; -1 up, +1 down; StageOrderFixed
    Task<Result<IReadOnlyList<PersonOption>>> FindPeopleAsync(string projectKey, string term,
        CancellationToken ct);                                    // Manage; any active account, at most 20
    Task<Result<StageView>> SetUpGateAsync(string stageKey, IReadOnlyList<Guid> approverIds, int? requiredApprovals,
        bool excludeRequester, byte[] expectedGateVersion, CancellationToken ct);
        // Manage; adds non-members as Viewers; GateRequestPending, GateApproved, Validation (number out of range)
    Task<Result<RequestPreview>> PreviewRequestAsync(string stageKey, CancellationToken ct);            // Contribute
    Task<Result<StageView>> RequestApprovalAsync(string stageKey, byte[] expectedGateVersion, CancellationToken ct);
        // Contribute; StageLocked, NoApprovers, ApproverCannotDecide, NotEnoughApprovers, GateApproved
    Task<Result<StageView>> WithdrawAsync(string stageKey, int requestNumber, CancellationToken ct);    // Contribute
    Task<Result<StageView>> DecideAsync(string stageKey, int requestNumber, GateDecisionKind decision, string? note,
        CancellationToken ct);
        // View + named approver (Forbidden otherwise, and for an excluded requester); AlreadyDecided;
        // Validation (note required to send back; at most 1,000 characters); Conflict when the request ended
}
public enum GateState { NotRequested, AwaitingDecision, Approved, SentBack }
public enum GateDecisionKind { Approve, SendBack }
public enum StagePlace { Approved, Current, Locked }
public sealed record StagesView(string ProjectKey, string ProjectName, bool CanContribute, bool CanManage,
    IReadOnlyList<StageView> Stages);
public sealed record StageView(string Key, string Title, StagePlace Place, DateOnly? StartDate, DateOnly? DueDate,
    Progress Progress, GateView Gate, byte[] StageVersion);
public sealed record GateView(GateState State, IReadOnlyList<ApproverView> Approvers, int ApprovalsNeeded,
    bool RequiredIsAll, bool ExcludeRequester, GateRequestView? CurrentRequest, bool CanDecide, byte[] Version);
public sealed record ApproverView(Guid UserId, string DisplayName, bool CanDecide);   // CanDecide: active member
public sealed record GateRequestView(int Number, string RequestedBy, Guid RequestedById, DateTimeOffset RequestedAt,
    IReadOnlyList<GateDecisionView> Decisions, string? SentBackNote);
public sealed record GateDecisionView(string ApproverName, GateDecisionKind Decision, string? Note, DateTimeOffset At);
public sealed record RequestPreview(string StageKey, IReadOnlyList<ContainedItemView> OpenItems);
public sealed record GateSummary(GateState State, int Approvals, int ApprovalsNeeded);   // in the stage's drawer
```

## Work module: dependencies

```csharp
public interface IDependencyService                               // FR-047–FR-052
{
    // Contribute on the project of both items (the same project; another project's item is NotFound).
    Task<Result<DependencyList>> AddAsync(string waitingKey, string waitedForKey, CancellationToken ct);
        // DependencyToSelf, DuplicateDependency, DependencyInHierarchy, DependencyLoop
    Task<Result<DependencyList>> RemoveAsync(string waitingKey, string waitedForKey, CancellationToken ct);
    Task<Result<IReadOnlyList<ItemOption>>> FindItemsAsync(string projectKey, string term, CancellationToken ct);
        // View; by key or words in the title, at most 20
}
// Both lists are of the item the drawer shows.
public sealed record DependencyList(IReadOnlyList<DependencyView> WaitsFor, IReadOnlyList<DependencyView> Blocks);
public sealed record DependencyView(string Key, WorkItemType Type, string Title, StatusOption Status,
    DateOnly? StartDate, DateOnly? DueDate, bool IsLate);
public sealed record ItemOption(string Key, WorkItemType Type, string Title);
```

## Work module: changed views

```csharp
// ITimelineService (changed, FR-042–FR-046, FR-050): TimelineView is organized in groups.
public sealed record TimelineView(string ProjectKey, string ProjectName, ProjectTemplate Template, bool CanContribute,
    bool CanManage, bool CanRestoreDeleted,
    IReadOnlyList<TimelineGroup> Groups,          // containers first (stage order, or epics by first date), then items
    int ScheduledCount,                           // in no container; at most 500 rows in all, as in Phase 2
    Page<TimelineItem> Unscheduled, bool HidingCompleted,
    IReadOnlyList<SprintBar> Sprints,             // Scrum: active and completed sprints
    IReadOnlyList<TimelineDependency> Dependencies);   // both ends shown; at most 200
public sealed record TimelineGroup(TimelineItem? Container, Progress? Progress, GateMarker? Gate,
    IReadOnlyList<TimelineRow> Rows, IReadOnlyList<TimelineItem> Unscheduled);
public sealed record GateMarker(DateOnly Date, GateState State);
public sealed record SprintBar(string Name, DateOnly StartDate, DateOnly EndDate, SprintState State);
public sealed record TimelineDependency(string WaitedForKey, string WaitingKey, bool IsLate);
// TimelineItem gains WorkItemType Type and a list of the keys it waits for (with late marks) for its accessible name;
// a milestone is scheduled when its date is set. RescheduleAsync refuses a start date on a milestone (InvalidDates).
// "Schedule" for containers and milestones is RescheduleAsync with the dates of FR-045, computed by the page.

// IWorkItemListService (changed, FR-020): WorkItemListQuery gains IReadOnlyList<WorkItemType>? Types and
// IReadOnlyList<string>? ContainerKeys plus bool NoContainer; WorkItemRow gains Type, ContainerRef? Container and
// decimal? Estimate; ListSort gains Type; the list shows every type; WorkItemListView gains ProjectTemplate Template.
// Every view model behind a project page carries the template for the project header from US1 on: BoardView,
// WorkItemListView, TimelineView (its Template field is added in US1; the rest of TimelineView changes in US4),
// TeamView and ProjectDetails.

// IMyTasksService (changed, FR-041): MyTaskRow gains WorkItemType Type; rows cover every type.
Task<Result<IReadOnlyList<WaitingGateRow>>> ListGatesAsync(CancellationToken ct);   // signed-in
public sealed record WaitingGateRow(string ProjectKey, string ProjectName, string StageKey, string StageTitle,
    string RequestedBy, DateTimeOffset RequestedAt);
```

## Error behavior summary

| Situation | Result |
|-----------|--------|
| A type the template does not offer, or a stage created by a Member | `TypeNotAllowed` / `Forbidden` |
| A type change other than among Story, Task and Bug in an agile project | `TypeChangeNotAllowed` |
| A container that is not a live epic or stage of the project, or on a container or sub-task | `InvalidContainer` |
| A sub-task under a milestone, epic or stage | `NoSubtasks` |
| Switching to or from Stage-gate | `TemplateSwitchNotAllowed` |
| Switching Scrum to Kanban with an active or planned sprint | `SprintsOpen` |
| Starting a second sprint | `SprintAlreadyActive` |
| Any change to a completed sprint, or moving a card into or out of it | `SprintCompleted` |
| Moving an item of a locked stage out of "to do", or a started item into a locked stage | `StageLocked`, naming the gate |
| A stage into "done" before its gate is approved | `GateNotApproved` |
| Deleting a stage, or changing a gate, after its approval | `GateApproved` |
| Changing a gate's setup while a request is pending | `GateRequestPending` |
| Requesting approval without approvers, with an approver who left, or without enough approvers besides an excluded requester | `NoApprovers`, `ApproverCannotDecide`, `NotEnoughApprovers` |
| A decision from someone who is not an approver, or from the excluded requester | `Forbidden` |
| A second decision by the same approver on one request | `AlreadyDecided` |
| A decision or withdrawal for a request that has ended | `Conflict` with the current gate |
| Moving an approved or the current stage, or putting a stage before them | `StageOrderFixed` |
| A dependency to itself, a repeat, within one hierarchy, or closing a loop | `DependencyToSelf`, `DuplicateDependency`, `DependencyInHierarchy`, `DependencyLoop` |
| An item of another project in a dependency | `NotFound` |
| Sprint or gate changed since it was read | `Conflict` with the current sprint or gate |
