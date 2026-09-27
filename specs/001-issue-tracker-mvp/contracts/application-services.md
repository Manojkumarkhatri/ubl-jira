# Contract: Application Services

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) |
**Permissions**: [permissions.md](./permissions.md)

Blazor components call these interfaces in-process (research R2); they are the system's behavioral
boundary and the primary target of integration tests. Signatures are C# and illustrative: parameter
objects may gain optional members during implementation, but the operations, permission rules, and
error codes below are the contract.

## Common conventions

```csharp
// Identity of the caller comes from ICurrentUser (never a parameter).
public enum ErrorKind { Validation, NotFound, Forbidden, Conflict, RuleViolation }

public sealed record AppError(
    ErrorKind Kind,
    string Code,                                        // stable, e.g. "SprintAlreadyActive"
    string Message,                                     // user-facing, plain language
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    object? Current = null);                            // latest state on Conflict (FR-024)

public readonly struct Result / Result<T> { /* Ok(value) or Error(AppError) */ }

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
// PageSize is fixed at 50 for issue lists and search (FR-026, FR-046).

public abstract record RankPlacement {                  // research R10
    public sealed record Before(string IssueKey) : RankPlacement;
    public sealed record After(string IssueKey)  : RankPlacement;
    public sealed record Top    : RankPlacement;  public sealed record Bottom : RankPlacement;
    public sealed record Up     : RankPlacement;  public sealed record Down   : RankPlacement;
}
```

- **Permission first**: each method checks permissions before any other work; no access → `NotFound`,
  insufficient role → `Forbidden` (see the matrix).
- **Versioned writes**: methods that take `expectedVersion` return `Conflict` with the current
  snapshot if the row changed since it was loaded (FR-024).
- **Validation** returns `Validation` with per-field messages; the UI keeps the user's input.
- **Rule violation codes**: `LastAdministrator`, `ProjectArchived`, `SprintAlreadyActive`,
  `SprintNotPlanned`, `SprintNotActive`, `BoardStyleLockedDuringSprint`, `InvalidHierarchy`,
  `TypeChangeNotSupported`, `AssigneeNotContributor`, `SetupClosed`, `InvalidSetupToken`,
  `FileTypeNotAllowed`, `FileTooLarge`, `AttachmentNotAvailable`, `DuplicateProjectKey`,
  `DuplicateProjectName`, `DuplicateUserName`, `DuplicateEmail`, `DuplicateFilterName`,
  `UserInactive`, `NotScrumProject`.
- **History and notifications**: every issue-changing method writes `IssueChange` rows, and where
  applicable `Notification`/`EmailOutbox` rows, in the same transaction.

## Identity module

```csharp
public interface ISetupService                                   // anonymous (FR-002)
{
    Task<bool> IsSetupOpenAsync(CancellationToken ct);
    Task<Result<Guid>> CreateFirstAdministratorAsync(
        string setupToken, string userName, string displayName, string email, string password,
        CancellationToken ct);                                   // SetupClosed, InvalidSetupToken
}

public interface IAccountService                                 // self (FR-006)
{
    Task<MyProfile> GetProfileAsync(CancellationToken ct);
    Task<Result> UpdateProfileAsync(
        string displayName, string? timeZoneId, bool emailNotificationsEnabled, CancellationToken ct);
    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct);
}

public interface IUserAdminService                               // Administrator (FR-003, FR-007)
{
    Task<Page<UserSummary>> ListUsersAsync(string? search, bool includeInactive, int page, CancellationToken ct);
    Task<Result<CreatedUser>> CreateUserAsync(                   // CreatedUser.TemporaryPassword shown once
        string userName, string displayName, string email, CancellationToken ct);
    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct);        // LastAdministrator
    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct);
    Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct); // new temporary password
    Task<Result> SetOrganizationRoleAsync(Guid userId, OrganizationRole role, CancellationToken ct);
}

public interface IAuditLogQuery                                  // Administrator (FR-011)
{
    Task<Result<Page<AuditEventView>>> QueryAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? userId, AuditEventType? type, int page,
        CancellationToken ct);
}

public interface IOrganizationSettingsService                   // read: all; write: Administrator
{
    Task<OrganizationSettingsView> GetAsync(CancellationToken ct);
    Task<Result> UpdateAsync(string defaultTimeZoneId, int idleTimeoutMinutes, byte[] expectedVersion,
        CancellationToken ct);                                   // FR-005, FR-051
}

public interface IAuditLog                                       // contract for other modules
{
    Task WriteAsync(AuditEventType type, Guid? subjectUserId, long? projectId, string target,
        object? details, CancellationToken ct);
}
```

## Projects module

```csharp
public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummary>> ListAccessibleAsync(bool includeArchived, CancellationToken ct); // FR-016
    Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct);
    Task<Result<string>> CreateAsync(string name, string key, string? description, BoardStyle style,
        CancellationToken ct);                                   // Admin; DuplicateProjectKey/Name (FR-012)
    Task<Result> UpdateAsync(string projectKey, string name, string? description, BoardStyle style,
        byte[] expectedVersion, CancellationToken ct);           // PA; BoardStyleLockedDuringSprint (FR-014)
    Task<Result> ArchiveAsync(string projectKey, CancellationToken ct);   // Admin (FR-015)
    Task<Result> RestoreAsync(string projectKey, CancellationToken ct);   // Admin
}

public interface IProjectMembershipService                       // PA (FR-014)
{
    Task<Result<IReadOnlyList<ProjectMemberView>>> ListMembersAsync(string projectKey, CancellationToken ct);
    Task<Result> AddMemberAsync(string projectKey, Guid userId, ProjectRole role, CancellationToken ct);
    Task<Result> ChangeRoleAsync(string projectKey, Guid userId, ProjectRole role, CancellationToken ct);
    Task<Result> RemoveMemberAsync(string projectKey, Guid userId, CancellationToken ct);
    Task<Result<IReadOnlyList<UserPick>>> SuggestAssigneesAsync(string projectKey, string? prefix,
        CancellationToken ct);                                   // active contributors only (FR-019)
}

public enum ProjectPermission { View, Contribute, Administer }

public interface IProjectAccess                                  // contract for other modules (R7)
{
    Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectPermission permission, CancellationToken ct);
    Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectPermission permission, CancellationToken ct);
    Task<IReadOnlyCollection<long>> AccessibleProjectIdsAsync(bool includeArchived, CancellationToken ct);
    Task<bool> CanViewAsync(Guid userId, long projectId, CancellationToken ct);  // mention/notify targets
}
public sealed record ProjectAccessInfo(long ProjectId, string Key, BoardStyle Style, bool IsArchived,
    ProjectRole? Role, bool IsAdministrator);
```

## Issues module

```csharp
public interface IIssueService
{
    Task<Result<string>> CreateAsync(string projectKey, NewIssue issue, CancellationToken ct);  // FR-017–FR-020
    Task<Result<IssueDetails>> GetAsync(string issueKey, CancellationToken ct);  // also records a recent view
    Task<Result<IssueDetails>> UpdateAsync(string issueKey, IssueEdit edit, byte[] expectedVersion,
        CancellationToken ct);                                   // FR-023, FR-024
    Task<Result<StatusChangeOutcome>> ChangeStatusAsync(string issueKey, IssueStatus status,
        byte[] expectedVersion, CancellationToken ct);           // FR-021, FR-022; OpenSubtasks warning
    Task<Result<IssueDetails>> AssignAsync(string issueKey, Guid? assigneeId, byte[] expectedVersion,
        CancellationToken ct);                                   // AssigneeNotContributor
    Task<Result<DeletePreview>> PreviewDeleteAsync(string issueKey, CancellationToken ct);  // sub-task count
    Task<Result> DeleteAsync(string issueKey, CancellationToken ct);                        // PA (FR-025)
    Task<Result<Page<DeletedIssueView>>> ListDeletedAsync(int page, CancellationToken ct);  // Admin
    Task<Result> RestoreAsync(long issueId, CancellationToken ct);                          // Admin
    Task<Result<Page<IssueListItem>>> ListProjectIssuesAsync(string projectKey, ProjectIssueQuery query,
        CancellationToken ct);                                   // FR-026
    Task<Result<IReadOnlyList<IssueChangeView>>> GetHistoryAsync(string issueKey, CancellationToken ct); // FR-037
}

public sealed record NewIssue(IssueType Type, string Summary, string? Description, Priority Priority,
    Guid? AssigneeId, IReadOnlyList<string> Labels, DateOnly? DueDate, decimal? Estimate, string? ParentKey);

public abstract record IssueEdit {                               // one field per save (FR-023)
    public sealed record Summary(string Value) : IssueEdit;
    public sealed record Description(string? Markdown) : IssueEdit;
    public sealed record Type(IssueType Value) : IssueEdit;       // TypeChangeNotSupported
    public sealed record Priority(Priority Value) : IssueEdit;
    public sealed record Labels(IReadOnlyList<string> Values) : IssueEdit;
    public sealed record DueDate(DateOnly? Value) : IssueEdit;
    public sealed record Estimate(decimal? Value) : IssueEdit;
    public sealed record Parent(string? ParentKey) : IssueEdit;   // InvalidHierarchy
}

public sealed record ProjectIssueQuery(IReadOnlyList<IssueType>? Types, IReadOnlyList<IssueStatus>? Statuses,
    IReadOnlyList<Priority>? Priorities, AssigneeFilter? Assignee, IReadOnlyList<string>? Labels,
    string? Text, IssueSort Sort, bool Descending, int Page);
public abstract record AssigneeFilter { Me; Unassigned; User(Guid Id) }

public interface ILabelService { Task<IReadOnlyList<string>> SuggestAsync(string prefix, CancellationToken ct); } // FR-027

public interface IAttachmentService                              // FR-053–FR-056
{
    Task<Result<AttachmentView>> UploadAsync(string issueKey, string fileName, Stream content, long length,
        CancellationToken ct);                                   // FileTypeNotAllowed, FileTooLarge; status Pending
    Task<Result<IReadOnlyList<AttachmentView>>> ListAsync(string issueKey, CancellationToken ct);
    Task<Result> RemoveAsync(long attachmentId, CancellationToken ct);           // uploader, PA, Admin
    Task<Result> PurgeAsync(long attachmentId, string reason, CancellationToken ct); // Admin; audited
    Task<Result<AttachmentDownload>> OpenAsync(long attachmentId, CancellationToken ct); // Clean only, else AttachmentNotAvailable
}

public interface IMalwareScanner                                 // research R17
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken ct);
}
public abstract record ScanResult { Clean; Infected(string Signature); Unavailable(string Reason) }

public interface IIssuePlanning                                  // contract for the Planning module
{
    Task<Result<IssueCardView>> MoveAsync(string issueKey, IssueStatus? toStatus, RankPlacement placement,
        byte[]? expectedVersion, CancellationToken ct);          // status change + rank in one change set
    Task SetSprintAsync(IReadOnlyCollection<long> issueIds, long? sprintId, CancellationToken ct); // sub-tasks follow
    Task<IReadOnlyList<IssueCardView>> CardsAsync(long projectId, CardScope scope, BoardFilter filter,
        CancellationToken ct);
}
```

## Collaboration module

```csharp
public interface ICommentService                                 // FR-033
{
    Task<Result<IReadOnlyList<CommentView>>> ListAsync(string issueKey, CancellationToken ct);
    Task<Result<CommentView>> AddAsync(string issueKey, string markdown, CancellationToken ct);  // auto-watch
    Task<Result<CommentView>> EditAsync(long commentId, string markdown, byte[] expectedVersion, CancellationToken ct);
    Task<Result> DeleteAsync(long commentId, CancellationToken ct);
}

public interface IMentionService                                 // FR-034
{
    Task<Result<IReadOnlyList<UserPick>>> SuggestAsync(string issueKey, string prefix, CancellationToken ct);
}

public interface IWatchService                                   // FR-035
{
    Task<Result<bool>> IsWatchingAsync(string issueKey, CancellationToken ct);
    Task<Result> WatchAsync(string issueKey, CancellationToken ct);
    Task<Result> UnwatchAsync(string issueKey, CancellationToken ct);
}

public interface INotificationService                            // FR-036
{
    Task<int> UnreadCountAsync(CancellationToken ct);
    Task<Page<NotificationView>> ListAsync(bool unreadOnly, int page, CancellationToken ct);
    Task MarkReadAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
}

public interface IIssueEventPublisher                            // contract used by Issues, inside the transaction
{
    Task PublishAsync(IssueEvent evt, CancellationToken ct);
    // IssueEvent: Assigned(issue, assignee, actor) | StatusChanged(issue, from, to, actor)
    //           | Mentioned(issue, users, actor, commentId?) | Commented(issue, commentId, actor)
    // Creates Notification rows for watchers/targets (never the actor) and EmailOutbox rows for
    // Assigned/Mentioned when the recipient is active and has emails on (FR-057).
}

public interface IEmailSender                                    // infrastructure; used by the outbox worker
{
    Task SendAsync(EmailMessage message, CancellationToken ct);  // throws on transient failure → retry (FR-058)
}
```

## Planning module

```csharp
public interface IBoardService                                   // FR-028–FR-032
{
    Task<Result<BoardView>> GetAsync(string projectKey, BoardFilter filter, CancellationToken ct);
    Task<Result<IssueCardView>> MoveCardAsync(string issueKey, IssueStatus toStatus, RankPlacement placement,
        byte[] expectedVersion, CancellationToken ct);           // Conflict if the card changed (US2 scenario 7)
}
public sealed record BoardFilter(AssigneeFilter? Assignee, IReadOnlyList<IssueType>? Types,
    IReadOnlyList<string>? Labels, string? EpicKey, string? Text);

public interface IBacklogService                                 // FR-038, FR-039
{
    Task<Result<BacklogView>> GetAsync(string projectKey, CancellationToken ct);   // NotScrumProject
    Task<Result> RankAsync(string issueKey, RankPlacement placement, CancellationToken ct);
    Task<Result> MoveToSprintAsync(string issueKey, long? sprintId, CancellationToken ct); // null = backlog
}

public interface ISprintService                                  // FR-040–FR-044
{
    Task<Result<SprintView>> CreateAsync(string projectKey, string? name, string? goal, CancellationToken ct);
    Task<Result<SprintView>> StartAsync(long sprintId, DateOnly startDate, DateOnly endDate, string? goal,
        byte[] expectedVersion, CancellationToken ct);           // SprintAlreadyActive, SprintNotPlanned
    Task<Result<SprintCompletionPreview>> PreviewCompletionAsync(long sprintId, CancellationToken ct);
    Task<Result> CompleteAsync(long sprintId, long? moveUnfinishedToSprintId, byte[] expectedVersion,
        CancellationToken ct);                                   // null = backlog; SprintNotActive
    Task<Result<SprintReport>> GetReportAsync(long sprintId, CancellationToken ct);
}
public sealed record SprintReport(SprintView Sprint, ScopeLine CommittedAtStart, ScopeLine Added,
    ScopeLine Removed, ScopeLine Completed, ScopeLine NotCompleted);
public sealed record ScopeLine(int IssueCount, decimal StoryPoints, IReadOnlyList<IssueListItem> Issues);
```

## Search module

```csharp
public interface IIssueSearchService                             // FR-045–FR-047
{
    Task<string?> ResolveKeyAsync(string text, CancellationToken ct);   // accessible issue key or null
    Task<Page<IssueListItem>> SearchAsync(IssueSearchCriteria criteria, IssueSort sort, bool descending,
        int page, CancellationToken ct);                         // default: UpdatedAt descending
}

public sealed record IssueSearchCriteria(
    string? Text, IReadOnlyList<string>? ProjectKeys, IReadOnlyList<IssueType>? Types,
    IReadOnlyList<IssueStatus>? Statuses, IReadOnlyList<Priority>? Priorities, AssigneeFilter? Assignee,
    IReadOnlyList<Guid>? Reporters, IReadOnlyList<string>? Labels, IReadOnlyList<long>? SprintIds,
    IReadOnlyList<string>? EpicKeys, DateRange? Created, DateRange? Updated, DateRange? Due,
    bool IncludeArchived);                                       // serialized into SavedFilter.CriteriaJson

public interface ISavedFilterService                             // FR-048
{
    Task<IReadOnlyList<SavedFilterView>> ListAsync(CancellationToken ct);
    Task<Result<long>> SaveAsync(string name, IssueSearchCriteria criteria, CancellationToken ct); // DuplicateFilterName
    Task<Result> RenameAsync(long filterId, string name, CancellationToken ct);
    Task<Result> DeleteAsync(long filterId, CancellationToken ct);
    Task<Result<SavedFilterView>> GetAsync(long filterId, CancellationToken ct);  // owner only, else NotFound
}

public interface IMyWorkService                                  // FR-049
{
    Task<MyWorkView> GetAsync(CancellationToken ct);             // open assigned issues by project + 10 recent views
}

public interface IRecentViews                                    // contract used by Issues
{
    Task RecordAsync(Guid userId, long issueId, CancellationToken ct);
}
```

## Traceability

| Module | Requirements covered |
|--------|----------------------|
| Identity | FR-001–FR-008, FR-011, FR-051 (settings) |
| Projects | FR-009, FR-010 (via `IProjectAccess`), FR-012–FR-016 |
| Issues | FR-017–FR-027, FR-037, FR-052 (rendering), FR-053–FR-056 |
| Collaboration | FR-033–FR-036, FR-057, FR-058 |
| Planning | FR-028–FR-032, FR-038–FR-044 |
| Search | FR-045–FR-049 |
| Web (cross-cutting) | FR-005 (idle UI), FR-050 (accessibility), FR-051 (display), FR-052 |
