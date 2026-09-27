# Contract: Application Services (Phase 1)

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) |
**Permissions**: [permissions.md](./permissions.md)

Blazor components call these interfaces in-process (research R2); they are the behavioral boundary
and the main target of integration tests. Signatures are illustrative C#: parameter objects may gain
optional members during implementation, but the operations, permission rules and error codes are the
contract.

## Common conventions

```csharp
// The caller's identity comes from ICurrentUser (never a parameter).
public enum ErrorKind { Validation, NotFound, Forbidden, Conflict, RuleViolation }
public sealed record AppError(ErrorKind Kind, string Code, string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null, object? Current = null);
public readonly struct Result / Result<T> { /* Ok(value) or Error(AppError) */ }

public abstract record CardPlacement {                 // research R14, R15
    public sealed record Before(string WorkItemKey) : CardPlacement;   // drop on a card
    public sealed record End   : CardPlacement;                        // drop on column footer
    public sealed record Top   : CardPlacement;                        // "Move to" menu
}

public sealed record PageRequest(int Page = 1, int PageSize = 50);    // 1-based; PageSize 1–100
public sealed record Page<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
```

- **Permission first**: every method checks `IProjectAccess` before any other work.
- **Versioned writes**: `expectedVersion` (row version) or `expectedBoardVersion` mismatches return
  `Conflict` carrying the current state (FR-022, FR-032, FR-041).
- **Rule violation codes**: `SetupClosed`, `InvalidSetupToken`, `DuplicateUserName`, `DuplicateEmail`,
  `LastAdministrator`, `DuplicateProjectKey`, `DuplicateProjectName`, `InvalidProjectKey`,
  `DuplicateColumnName`, `TooManyColumns`, `LastToDoColumn`, `LastDoneColumn`, `ColumnNotEmpty`,
  `DestinationRequired`, `SubtaskDepth`, `CommentNotOwned`.
- **History**: every work-item-changing method writes `WorkItemChange` rows in the same transaction.
- **Paging**: every list is paged (constitution performance baseline): screens show 50 items per page;
  the drawer loads sub-tasks, comments and history 50 at a time with "Show more".

## Identity module

```csharp
public interface ISetupService                                   // anonymous (FR-002)
{
    Task<bool> IsSetupOpenAsync(CancellationToken ct);
    Task<Result<Guid>> CreateFirstAdministratorAsync(string setupToken, string userName,
        string displayName, string email, string password, CancellationToken ct);
}

public interface IAccountService                                 // self (FR-007)
{
    Task<MyProfile> GetProfileAsync(CancellationToken ct);
    Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct); // FR-007, FR-043
    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct);
}

public interface IUserAdminService                               // Administrator (FR-003, FR-004)
{
    Task<Page<UserSummary>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct);
    Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email,
        CancellationToken ct);                                   // TemporaryPassword returned once
    Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct);  // new temporary password
    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct);             // LastAdministrator
    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct);
}

public interface IAuditLog                                       // contract for other modules (FR-010)
{
    Task WriteAsync(AuditEventType type, Guid? subjectUserId, string target, object? details,
        CancellationToken ct);
}

public interface IUserDirectory                                  // contract for other modules
{
    Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken ct);           // deactivated users included: their names stay on past work
}
public sealed record UserDisplay(Guid Id, string DisplayName, bool IsActive);
```

## Projects module

```csharp
public interface IProjectService
{
    Task<Page<ProjectSummary>> ListAsync(PageRequest page, CancellationToken ct); // FR-013, by name
    Task<string> SuggestKeyAsync(string projectName, CancellationToken ct);      // FR-011
    Task<Result<string>> CreateAsync(string name, string key, string? description,
        CancellationToken ct);            // DuplicateProjectKey/Name, InvalidProjectKey; seeds 3 columns (FR-016)
    Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct);
    Task<Result> UpdateDetailsAsync(string projectKey, string name, string? description,
        int expectedDetailsVersion, CancellationToken ct);       // Owner/Admin (FR-014)
}
public sealed record ProjectSummary(string Key, string Name, string OwnerDisplayName, int OpenItemCount);

public interface IBoardColumnService                             // Owner/Admin (FR-034–FR-039, FR-041)
{
    Task<Result<BoardColumnsView>> GetAsync(string projectKey, CancellationToken ct);
    Task<Result<BoardColumnsView>> AddAsync(string projectKey, string name, StatusCategory category,
        int position, int expectedBoardVersion, CancellationToken ct);    // DuplicateColumnName, TooManyColumns
    Task<Result<BoardColumnsView>> RenameAsync(string projectKey, long columnId, string name,
        int expectedBoardVersion, CancellationToken ct);
    Task<Result<BoardColumnsView>> MoveAsync(string projectKey, long columnId, int newPosition,
        int expectedBoardVersion, CancellationToken ct);
    Task<Result<BoardColumnsView>> SetWipLimitAsync(string projectKey, long columnId, int? limit,
        int expectedBoardVersion, CancellationToken ct);         // 1–99 or null
    Task<Result<BoardColumnsView>> ChangeCategoryAsync(string projectKey, long columnId,
        StatusCategory category, int expectedBoardVersion, CancellationToken ct);  // ColumnNotEmpty, LastToDo/DoneColumn
    Task<Result<BoardColumnsView>> DeleteAsync(string projectKey, long columnId,
        long? destinationColumnId, int expectedBoardVersion, CancellationToken ct); // DestinationRequired, LastToDo/DoneColumn
}
public enum StatusCategory { ToDo, InProgress, Done }

public enum ProjectRight { View, Contribute, DeleteOwnWorkItem, Manage, Restore }
public interface IProjectAccess                                  // contract for other modules (research R7)
{
    Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct);
    Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectRight right, CancellationToken ct);
    Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct);
}
public sealed record ProjectAccessInfo(long ProjectId, string Key, bool CanManage, bool IsAdministrator);

public interface IProjectWorkflow                                // contract for the Work module
{
    Task<IReadOnlyList<StatusInfo>> StatusesAsync(long projectId, CancellationToken ct);
    Task<StatusInfo> FirstToDoStatusAsync(long projectId, CancellationToken ct);
}
public sealed record StatusInfo(long Id, string Name, StatusCategory Category, int Position, int? WipLimit);
```

## Work module

```csharp
public interface IBoardService                                   // FR-016–FR-023
{
    Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct);
    Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title,
        CancellationToken ct);                                   // appended to the column (FR-018)
    Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement,
        byte[] expectedVersion, CancellationToken ct);           // Conflict if the card changed (FR-022)
}
public sealed record BoardView(string ProjectKey, int BoardVersion, bool CanManageColumns,
    IReadOnlyList<ColumnView> Columns);
public sealed record ColumnView(long Id, string Name, StatusCategory Category, int? WipLimit,
    int CardCount, bool OverLimit, IReadOnlyList<CardView> Cards);
public sealed record CardView(string Key, string Title, Priority Priority, int SubtasksDone,
    int SubtasksTotal, byte[] Version);

public interface IWorkItemService                                // FR-024–FR-029, FR-031–FR-033
{
    Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct);
    Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit,
        byte[] expectedVersion, CancellationToken ct);
    Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title,
        CancellationToken ct);                                   // SubtaskDepth; starts in first "to do"
    Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion,
        CancellationToken ct);                                   // leftmost "done" column
    Task<Result<DeletePreview>> PreviewDeleteAsync(string workItemKey, CancellationToken ct);
    Task<Result> DeleteAsync(string workItemKey, CancellationToken ct);          // Creator/Owner/Admin
    Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page,
        CancellationToken ct);                                   // Admin
    Task<Result> RestoreAsync(string workItemKey, CancellationToken ct);         // Admin
    Task<Result<Page<SubtaskView>>> ListSubtasksAsync(string parentKey, PageRequest page,
        CancellationToken ct);                                   // rank order
    Task<Result<Page<ChangeView>>> GetHistoryAsync(string workItemKey, PageRequest page,
        CancellationToken ct);                                   // time order
    // GetAsync returns WorkItemDetails with the first page (50) of sub-tasks, comments and history.
}
public abstract record WorkItemEdit {                            // one field per save (FR-026)
    public sealed record Title(string Value) : WorkItemEdit;
    public sealed record Description(string? Value) : WorkItemEdit;
    public sealed record Priority(Priority Value) : WorkItemEdit;
    public sealed record Status(long ColumnId) : WorkItemEdit;   // result warns about open sub-tasks (FR-029)
}

public interface ICommentService                                 // FR-030
{
    Task<Result<Page<CommentView>>> ListAsync(string workItemKey, PageRequest page,
        CancellationToken ct);                                   // oldest first
    Task<Result<CommentView>> AddAsync(string workItemKey, string body, CancellationToken ct);
    Task<Result<CommentView>> EditAsync(long commentId, string body, byte[] expectedVersion,
        CancellationToken ct);                                   // CommentNotOwned
    Task<Result> DeleteAsync(long commentId, CancellationToken ct);             // CommentNotOwned
}

public interface IWorkItemCounts                                 // contract used by the Projects module
{
    Task<IReadOnlyDictionary<long, int>> CountByStatusAsync(IReadOnlyCollection<long> statusIds,
        CancellationToken ct);           // non-deleted tasks and sub-tasks per status (FR-013 open counts)
}

public interface IWorkItemStatusMover                            // contract used by the Projects module
{
    Task<int> CountInStatusAsync(long statusId, CancellationToken ct);           // includes deleted items
    Task MoveAllAsync(long fromStatusId, long toStatusId, string note, CancellationToken ct); // FR-037
}
```

## Traceability

| Module | Requirements |
|--------|--------------|
| Identity | FR-001–FR-008, FR-010 |
| Projects | FR-009 (via `IProjectAccess`), FR-011–FR-016, FR-034–FR-041 |
| Work | FR-017–FR-033 |
| Web (cross-cutting) | FR-006 (idle warning), FR-042–FR-044 |
