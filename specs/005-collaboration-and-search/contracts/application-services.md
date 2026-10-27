# Contract: Application Services (Phase 4)

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) |
**Permissions**: [permissions.md](./permissions.md)

This contract lists what Phase 4 adds to or changes in the Phase 1–3 services (`specs/002-…`, `specs/003-…`,
`specs/004-…/contracts/application-services.md`); everything not mentioned stays as it was. The conventions are
unchanged: the caller comes from `ICurrentUser`; every project-scoped method checks `IProjectAccess` first;
versioned writes return `Conflict` with the current state; lists are paged. The rights named below (`View`,
`Contribute`, `Manage`) are those of `IProjectAccess`. Signatures are illustrative C#; the operations, permission
rules and error codes are the contract.

**New rule and validation codes**: `UnsupportedFormatting`, `InvalidLink`, `FileTypeNotAllowed`, `FileTooLarge`,
`FileContentMismatch`, `AttachmentNotAvailable`, `InvalidLabel`, `TooManyLabels`, `NotAMember`, `FilterNameTaken`.
`TextTooLong` (Phase 1) now also covers documents over 512 KB.

## Identity module

```csharp
public enum EmailChoice { None, AimedAtMe, All }                 // Upms.Domain.Common, like NotificationKind (research R11)

public interface IAccountService                                  // changed
{
    // MyProfile gains EmailChoice.
    Task<Result<MyProfile>> SetEmailChoiceAsync(EmailChoice choice, CancellationToken ct);   // own account only
}

public interface IUserDirectory                                   // Identity.Contracts, changed
{
    // The facts the Notifications module needs to address an event's recipients.
    Task<IReadOnlyDictionary<Guid, RecipientInfo>> GetRecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
public sealed record RecipientInfo(Guid Id, string DisplayName, string Email, bool IsActive, EmailChoice EmailChoice);

public interface IContentAuditLog                                 // Identity.Contracts, new (research R20)
{
    // Writes the matching AuditEventType in the caller's transaction.
    Task WriteAsync(ContentAuditEvent change, string target, object details, CancellationToken ct);
}
public enum ContentAuditEvent { MalwareDetected, AttachmentPurged, EmailGivenUp }
```

## Projects module

```csharp
public interface IProjectAccess                                   // Projects.Contracts, changed
{
    // Every project the caller can see: their active memberships, or null for administrators (all projects).
    Task<IReadOnlySet<long>?> VisibleProjectIdsAsync(CancellationToken ct);
}
public interface IProjectTeam                                     // Projects.Contracts, changed
{
    // Active members (any role) matching a display name or user name, at most `take` (mention suggestions).
    Task<IReadOnlyList<TeamMemberInfo>> FindMembersAsync(long projectId, string term, int take, CancellationToken ct);
    // Which of these people can see the project now (members, or administrators).
    Task<IReadOnlySet<Guid>> WhoCanSeeAsync(long projectId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
```

## Notifications module (new)

```csharp
public enum NotificationKind { Assigned, Mentioned, Commented, StatusChanged, ApprovalRequested, GateDecided, UploadBlocked }

public interface INotificationCenter                              // Notifications.Contracts, used by the Work module
{
    // Adds the notifications (and emails) of one action to the caller's unit of work; the caller saves.
    // Removes the actor and anyone who cannot see the project now, and keeps one reason per person
    // (Mentioned > Assigned > Commented or StatusChanged) (research R8).
    Task RaiseAsync(IReadOnlyCollection<NotificationEvent> action, CancellationToken ct);
}
public sealed record NotificationEvent(NotificationKind Kind, long ProjectId, long WorkItemId, Guid? ActorId,
    IReadOnlyCollection<Guid> Recipients, string? Detail = null);

public interface INotificationService                             // the bell and the panel (own notifications only)
{
    Task<int> CountUnreadAsync(CancellationToken ct);
    Task<Page<NotificationView>> ListAsync(PageRequest page, CancellationToken ct);           // newest first, 20 a page
    Task<Result> MarkAsync(long notificationId, bool read, CancellationToken ct);           // NotFound if not the caller's
    Task MarkAllReadAsync(CancellationToken ct);
}
public sealed record NotificationView(long Id, NotificationKind Kind, string? ActorName, string ProjectKey, string ItemKey,
    string ItemTitle, string? Detail, DateTimeOffset OccurredAt, bool IsRead);
```

Background workers (Infrastructure, research R9, R10): `EmailDispatchWorker` (every 15 s) and
`NotificationCleanupWorker` (daily). The email transport is `IEmailTransport` (MailKit in production, a fake in
tests).

## Work module

### Formatted text

```csharp
public sealed record RichText(TextFormat Format, string Content, string PlainText);        // Content: plain text or JSON
public enum TextFormat { Plain, Document }
```

- `WorkItemEdit.Description(string? Value)` now carries a document's JSON (or null to clear). The service parses
  it with `FormattedText.Parse` (`UnsupportedFormatting`, `InvalidLink`, `TextTooLong`), keeps mentions of active
  members only, records history with plain text, rewrites search terms, and raises `Mentioned` for new mentions.
- `WorkItemDetails.Description` becomes `RichText?`.
- `ICommentService.AddAsync(workItemKey, string documentJson)` and `EditAsync(commentId, string documentJson,
  expectedVersion)`: same checks. `AddAsync` makes the author a watcher and raises `Commented` for the watchers
  and `Mentioned` for new mentions. `CommentView` gains `RichText Body` in place of the plain string.
- A status change (board, drawer, "mark done", column deletion) raises `StatusChanged` for the watchers. An
  assignment to someone else raises `Assigned` and makes the assignee a watcher.

### Mentions and watchers

```csharp
public interface IMentionService
{
    // Contribute. Active members of the item's project, by display name or user name, at most 10 (FR-008).
    Task<Result<IReadOnlyList<PersonOption>>> SuggestAsync(string workItemKey, string term, CancellationToken ct);
}
public interface IWatcherService
{
    Task<Result<WatchersView>> GetAsync(string workItemKey, CancellationToken ct);           // View
    Task<Result<WatchersView>> WatchAsync(string workItemKey, CancellationToken ct);         // View; idempotent
    Task<Result<WatchersView>> StopWatchingAsync(string workItemKey, CancellationToken ct);  // View; idempotent
    Task<Result<WatchersView>> AddAsync(string workItemKey, Guid userId, CancellationToken ct);     // Manage; NotAMember
    Task<Result<WatchersView>> RemoveAsync(string workItemKey, Guid userId, CancellationToken ct);  // Manage
}
public sealed record WatchersView(int Count, bool IsWatching, IReadOnlyList<PersonOption> People, bool CanManage);
```

`WorkItemDetails` gains `WatchersView Watchers`, `IReadOnlyList<AttachmentView> Attachments` and
`IReadOnlyList<string> Labels`. Opening the drawer (`IWorkItemService.GetAsync`) also records a recent view.

### Attachments

```csharp
public interface IAttachmentService
{
    Task<Result<IReadOnlyList<AttachmentView>>> ListAsync(string workItemKey, CancellationToken ct);       // View
    // Contribute. Checks the type, the size (25 MB) and the first bytes, streams the file into the database as
    // "being checked", records history (FileTypeNotAllowed, FileTooLarge, FileContentMismatch) (research R13).
    Task<Result<AttachmentView>> AddAsync(string workItemKey, string fileName, long size, Stream content, CancellationToken ct);
    Task<Result> RemoveAsync(long attachmentId, CancellationToken ct);    // uploader with Contribute, Manage, or Admin
    Task<Result> PurgeAsync(long attachmentId, CancellationToken ct);     // Admin only; audit AttachmentPurged
    // View at this moment; Passed, not removed or purged, item not deleted; otherwise NotFound (FR-027).
    Task<Result<AttachmentFile>> OpenAsync(long attachmentId, bool preview, CancellationToken ct);
}
public sealed record AttachmentView(long Id, string FileName, long Size, string UploadedBy, DateTimeOffset UploadedAt,
    AttachmentCheckStatus Status, bool IsImage, bool CanRemove);
public sealed record AttachmentFile(string FileName, string ContentType, long Size, Stream Content);   // caller disposes
```

`AttachmentScanWorker` (Infrastructure, research R14) uses `IMalwareScanner` (`ClamAvScanner`): clean → `Passed`;
infected → `Blocked` (quarantined: content kept, never served), `UploadBlocked` raised for the uploader, audit `MalwareDetected`;
unavailable → retried.

### Labels

```csharp
public interface ILabelService
{
    Task<Result<IReadOnlyList<string>>> SuggestAsync(string projectKey, string term, CancellationToken ct);  // Contribute;
                                                                   // labels used in projects the caller can see
    Task<Result<IReadOnlyList<string>>> AddAsync(string workItemKey, string label, CancellationToken ct);  // Contribute;
                                                                   // InvalidLabel, TooManyLabels; reuses any case
    Task<Result<IReadOnlyList<string>>> RemoveAsync(string workItemKey, string label, CancellationToken ct);
}
```

- `CardView` gains `IReadOnlyList<string> Labels` and `ContainerView? Container` (board quick filters, research
  R19).
- `WorkItemListQuery` gains `IReadOnlyList<string>? Labels` (items with any of them); `WorkItemRow` gains
  `Labels`. The list's word filter now reads `DescriptionText` (research R4).

### Search, recent items and saved filters

```csharp
public interface ISearchService
{
    // A key the caller can see opens it; any other key (unknown or hidden) → NotFound with the same message (FR-032).
    // All three refuse a signed-out or deactivated caller (Forbidden).
    Task<Result<string>> ResolveKeyAsync(string text, CancellationToken ct);
    Task<Result<IReadOnlyList<SearchResultRow>>> RecentAsync(CancellationToken ct);          // 10, visible only
    Task<Result<SearchResults>> SearchAsync(SearchQuery query, PageRequest page, CancellationToken ct);
}
public sealed record SearchQuery(string? Words, IReadOnlyList<string>? ProjectKeys = null, IReadOnlyList<WorkItemType>? Types = null,
    IReadOnlyList<StatusCategory>? Categories = null, IReadOnlyList<Priority>? Priorities = null, bool AssignedToMe = false,
    bool Unassigned = false, IReadOnlyList<Guid>? Assignees = null, IReadOnlyList<Guid>? Creators = null,
    IReadOnlyList<string>? Labels = null, DateRange? Created = null, DateRange? Updated = null, DateRange? Due = null,
    SearchSort Sort = SearchSort.Updated, bool Descending = true);
public enum SearchSort { Updated, Created, Key, Priority, DueDate }
public sealed record SearchResults(Page<SearchResultRow> Rows, IReadOnlyList<string> UnavailableValues);
public sealed record SearchResultRow(string Key, WorkItemType Type, string Title, string ProjectKey, string ProjectName,
    StatusOption Status, AssigneeRef? Assignee, IReadOnlyList<string> Labels, DateTimeOffset UpdatedAt);

public interface ISavedFilterService
{
    Task<SavedFilterList> ListAsync(CancellationToken ct);                   // own, then shared with the caller
    Task<Result<SavedFilterView>> GetAsync(long filterId, CancellationToken ct);            // owner or a share's member
    Task<Result<SavedFilterView>> SaveAsync(string name, string query, CancellationToken ct);            // FilterNameTaken
    Task<Result<SavedFilterView>> RenameAsync(long filterId, string name, byte[] expectedVersion, CancellationToken ct);
    Task<Result<SavedFilterView>> UpdateAsync(long filterId, string query, byte[] expectedVersion, CancellationToken ct);
    Task<Result> DeleteAsync(long filterId, CancellationToken ct);
    Task<Result<SavedFilterView>> ShareAsync(long filterId, IReadOnlyList<string> projectKeys, CancellationToken ct);
                                                                   // replaces the shares; NotAMember (owner only)
}
public sealed record SavedFilterView(long Id, string Name, string Query, string OwnerName, bool IsMine,
    IReadOnlyList<ProjectRef> SharedWith, byte[] Version);
public sealed record SavedFilterList(IReadOnlyList<SavedFilterView> Mine, IReadOnlyList<SavedFilterView> SharedWithMe);
```

`SearchIndexWorker` (Infrastructure) fills `SearchTerms` for items that have none, 500 at a time in order of their ids,
keeping its place in `SearchIndexProgress` (research R15).

## Error codes (summary)

| Code | When |
|------|------|
| `UnsupportedFormatting` | a document with an unknown node or mark, nested tables, or too many rows or columns |
| `InvalidLink` | a link that is not `http`, `https` or `mailto` |
| `TextTooLong` | more than 32,000 characters of text, or a document over 512 KB |
| `FileTypeNotAllowed` | an extension outside FR-024's list |
| `FileTooLarge` | a file over 25 MB |
| `FileContentMismatch` | the first bytes do not match the extension |
| `AttachmentNotAvailable` | removing a file already removed or purged |
| `InvalidLabel` | an empty label, one with whitespace or control characters, or over 50 characters |
| `TooManyLabels` | a 21st label on one item |
| `NotAMember` | adding a watcher, or sharing a filter with a project, for someone who is not a member |
| `FilterNameTaken` | a second filter with the same name for one owner |
