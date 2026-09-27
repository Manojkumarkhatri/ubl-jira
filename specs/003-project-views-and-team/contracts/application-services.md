# Contract: Application Services (Phase 2)

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) |
**Permissions**: [permissions.md](./permissions.md)

This contract lists what Phase 2 adds to or changes in the Phase 1 services
(`specs/002-kanban-project-core/contracts/application-services.md`); everything not mentioned stays as it
was. The conventions are unchanged: the caller comes from `ICurrentUser`; every method checks
`IProjectAccess` first; versioned writes return `Conflict` with the current state; lists are paged 50 at a
time. Signatures are illustrative C#; the operations, permission rules and error codes are the contract.

**New rule violation codes**: `DuplicateMember`, `LastProjectAdmin`, `NotAssignable`, `InvalidDates`
(the existing `AccountDeactivated` is reused when a deactivated account is chosen as a member).

## Identity module

```csharp
public interface IUserDirectory                                   // contract for other modules
{
    Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    // New: active accounts whose display name, user name or email address contains the term (at most `take`,
    // ordered by display name); used by the members screen (research R5).
    Task<IReadOnlyList<UserDisplay>> SearchActiveAsync(string term, int take, CancellationToken ct);
}
public sealed record UserDisplay(Guid Id, string DisplayName, bool IsActive, string UserName);   // UserName is new

public enum AuditEventType { /* Phase 1 values */ MemberAdded, MemberRemoved, MemberRoleChanged }  // new values

public interface IMembershipAuditLog                              // contract for the Projects module (research R6)
{
    // Writes MemberAdded, MemberRemoved or MemberRoleChanged in the caller's transaction:
    // subject = the member, target = the project key, details = the roles.
    Task WriteAsync(MembershipChange change, Guid memberId, string projectKey, object details, CancellationToken ct);
}
public enum MembershipChange { Added, Removed, RoleChanged }
```

## Projects module

```csharp
public enum ProjectRole { ProjectAdmin, Member, Viewer }

public interface IProjectMemberService                            // FR-001, FR-008–FR-013
{
    Task<Result<TeamView>> GetTeamAsync(string projectKey, CancellationToken ct);             // any member (View)
    Task<Result<IReadOnlyList<PersonOption>>> FindPeopleAsync(string projectKey, string term,
        CancellationToken ct);                // Manage; active accounts not yet members, at most 20
    Task<Result<TeamView>> AddAsync(string projectKey, Guid userId, ProjectRole role,
        int expectedMembersVersion, CancellationToken ct);       // Manage; DuplicateMember, AccountDeactivated
    Task<Result<TeamView>> ChangeRoleAsync(string projectKey, Guid userId, ProjectRole role,
        int expectedMembersVersion, CancellationToken ct);       // Manage; LastProjectAdmin, NotFound (member)
    Task<Result<TeamView>> RemoveAsync(string projectKey, Guid userId,
        int expectedMembersVersion, CancellationToken ct);       // Manage; LastProjectAdmin, NotFound (member)
}
public sealed record TeamView(string ProjectKey, string ProjectName, int MembersVersion, bool CanManage,
    IReadOnlyList<MemberView> Members);                           // Project Admins first, then by display name
public sealed record MemberView(Guid UserId, string DisplayName, string UserName, ProjectRole Role,
    bool IsActive, bool IsMe, DateTimeOffset AddedAt);
public sealed record PersonOption(Guid UserId, string DisplayName, string UserName);

// Changed: the owner column becomes the caller's role (FR-015).
public sealed record ProjectSummary(string Key, string Name, ProjectRole? MyRole, bool AdministratorAccess,
    int OpenItemCount);
// IProjectService.ListAsync lists only the projects the caller can see (members; administrators see all).
// IProjectService.CreateAsync makes the caller the first Project Admin (FR-007).

// Changed: membership rules (research R2). Non-members get NotFound; members without the right get Forbidden.
public sealed record ProjectAccessInfo(long ProjectId, string Key, Guid UserId, ProjectRole? Role,
    bool CanContribute, bool CanManage, bool IsAdministrator);

public interface IProjectTeam                                     // new contract for the Work module (research R7)
{
    // Members with role and account status; for assignee choices and "can still work" marks.
    Task<IReadOnlyList<TeamMemberInfo>> GetMembersAsync(long projectId, CancellationToken ct);
    // True when the person is an active Project Admin or Member of the project (FR-016).
    Task<bool> CanBeAssignedAsync(long projectId, Guid userId, CancellationToken ct);
    // The projects among `projectIds` that the person can see (member, or administrator), with names.
    Task<IReadOnlyList<ProjectRef>> VisibleAmongAsync(Guid userId, IReadOnlyCollection<long> projectIds,
        CancellationToken ct);
}
// CanWork: an active Project Admin or Member. The role itself is not exposed, so the Work module never depends on
// the Projects domain's ProjectRole (module rule).
public sealed record TeamMemberInfo(Guid UserId, string DisplayName, bool CanWork);
public sealed record ProjectRef(long Id, string Key, string Name);

// IProjectWorkflow (Phase 1 contract) gains a batch read for "My tasks":
//   Task<IReadOnlyDictionary<long, IReadOnlyList<StatusInfo>>> StatusesAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct);
```

## Work module

```csharp
// Drawer (IWorkItemService): two new edits; both are Contribute and versioned like the Phase 1 edits.
public abstract record WorkItemEdit
{
    // Phase 1: Title, Description, Priority, Status
    public sealed record Assignee(Guid? UserId) : WorkItemEdit;              // NotAssignable (FR-016, FR-017)
    public sealed record Dates(DateOnly? Start, DateOnly? Due) : WorkItemEdit; // InvalidDates (FR-018, FR-019)
}

public sealed record AssigneeRef(Guid UserId, string DisplayName, string Initials, bool CanWork);  // FR-021, FR-024
                                            // Initials: first letters of the first and last words ("Amina Khan" → "AK")
public sealed record AssigneeOption(Guid UserId, string DisplayName, bool IsMe);

// WorkItemDetails gains:
//   AssigneeRef? Assignee, DateOnly? StartDate, DateOnly? DueDate,
//   bool CanContribute (false: read-only drawer, FR-003),
//   IReadOnlyList<AssigneeOption> AssigneeOptions (active Project Admins and Members; empty for Viewers).
// SubtaskView gains: AssigneeRef? Assignee, DateOnly? DueDate.

// Board (IBoardService): CardView gains AssigneeRef? Assignee and DateOnly? DueDate;
// BoardView gains bool CanContribute and Guid ViewerId (for "Only my tasks"). Filters run in the component.

public interface IWorkItemListService                             // FR-027–FR-032
{
    Task<Result<WorkItemListView>> ListAsync(string projectKey, WorkItemListQuery query, DateOnly today,
        CancellationToken ct);                                    // View
}
public enum ListSort { Key, Title, Status, Priority, Assignee, StartDate, DueDate, Updated }
public enum DueFilter { Any, Overdue, Next7Days, NoDueDate }   // Overdue: open, due < today;
                                                              // Next7Days: open, today <= due <= today + 7
public sealed record WorkItemListQuery(
    ListSort Sort = ListSort.Key, bool Descending = true,          // newest key first by default
    IReadOnlyList<long>? ColumnIds = null, IReadOnlyList<StatusCategory>? Categories = null,
    IReadOnlyList<Priority>? Priorities = null,
    bool AssignedToMe = false, bool Unassigned = false, IReadOnlyList<Guid>? AssigneeIds = null,
    DueFilter Due = DueFilter.Any, string? Text = null, int Page = 1);
// Sorting: priority ascending means Highest first; dates put undated items last in both directions; equal values
// keep key order in the same direction; assignee sorts put unassigned items last.
// People: the team plus anyone else the project's tasks are assigned to (for "what did bilal work on?").
// CanManage and CanRestoreDeleted drive the project header's links.
public sealed record WorkItemListView(string ProjectKey, string ProjectName, bool CanContribute, bool CanManage,
    bool CanRestoreDeleted, long FirstToDoColumnId, IReadOnlyList<StatusOption> Columns,
    IReadOnlyList<AssigneeOption> People, Page<WorkItemRow> Rows);
public sealed record WorkItemRow(string Key, string Title, string? ParentKey, StatusOption Status,
    Priority Priority, AssigneeRef? Assignee, DateOnly? StartDate, DateOnly? DueDate, DateTimeOffset UpdatedAt,
    bool IsOpen);
// Adding a task from the list uses IBoardService.CreateInlineAsync with FirstToDoColumnId (FR-033).

public interface ITimelineService                                 // FR-034–FR-040
{
    Task<Result<TimelineView>> GetAsync(string projectKey, bool hideCompleted, CancellationToken ct);   // View
    Task<Result<Page<TimelineItem>>> ListUnscheduledAsync(string projectKey, PageRequest page,
        CancellationToken ct);                                                                          // View
    // Contribute; the same rules and conflict check as WorkItemEdit.Dates; Conflict carries the current item.
    Task<Result<TimelineItem>> RescheduleAsync(string workItemKey, DateOnly? start, DateOnly? due,
        byte[] expectedVersion, CancellationToken ct);            // InvalidDates
}
public sealed record TimelineView(string ProjectKey, string ProjectName, bool CanContribute,
    IReadOnlyList<TimelineRow> Rows,                              // by first date, then due date, then key
    Page<TimelineItem> Unscheduled, bool HidingCompleted);
public sealed record TimelineRow(TimelineItem Task, IReadOnlyList<TimelineItem> ScheduledSubtasks,
    IReadOnlyList<TimelineItem> UnscheduledSubtasks);
public sealed record TimelineItem(string Key, string Title, StatusOption Status, AssigneeRef? Assignee,
    DateOnly? StartDate, DateOnly? DueDate, bool IsOpen, byte[] Version);

public interface IMyTasksService                                  // FR-025, FR-026
{
    // The caller's open, assigned tasks and sub-tasks in projects they can see, ordered by project name,
    // due date (undated last), priority, key.
    Task<Result<Page<MyTaskRow>>> ListAsync(PageRequest page, CancellationToken ct);
}
public sealed record MyTaskRow(string ProjectKey, string ProjectName, string Key, string Title, string? ParentKey,
    StatusOption Status, Priority Priority, DateOnly? DueDate);
```

## Error behavior summary

| Situation | Result |
|-----------|--------|
| Caller is not a member (and not an administrator) of the project, for any project-scoped call | `NotFound` (as for an unknown project) |
| Viewer tries to change anything | `Forbidden` |
| Member tries to manage the project, its columns or its team | `Forbidden` |
| Assigning someone who is not an active Project Admin or Member | `NotAssignable` (the drawer reloads its choices) |
| Due date before start date, or a date outside 2000–2099 | `InvalidDates`, with a field error |
| Adding an existing member | `DuplicateMember`, naming the current role |
| Removing or retyping the last active Project Admin | `LastProjectAdmin` |
| Team changed since it was read (`MembersVersion`) | `Conflict` with the current `TeamView` |
| Task changed since it was read (drawer, timeline) | `Conflict` with the current task |
