using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A task's details drawer (FR-023 to FR-033; Phase 2 FR-016–FR-019, FR-021, FR-023, FR-024).</summary>
public interface IWorkItemService
{
    /// <summary>Details with the first page (50) of sub-tasks, comments and history.</summary>
    Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct);

    /// <summary>A work item of any type the template offers, from the list, the backlog or a container's drawer, in the
    /// leftmost "to do" column (Phase 3 FR-010, FR-015): <c>TypeNotAllowed</c>, <c>InvalidContainer</c>. Stages need the
    /// Manage right (FR-039).</summary>
    Task<Result<WorkItemDetails>> CreateAsync(string projectKey, NewWorkItem item, CancellationToken ct);

    /// <summary>The project's epics (by key) or stages (in stage order), for the drawer's "Epic" or "Stage" choice.</summary>
    Task<Result<IReadOnlyList<ContainerOption>>> ListContainersAsync(string projectKey, CancellationToken ct);

    /// <summary>Saves one field (FR-026); <c>Conflict</c> with the latest values when someone else changed the
    /// task first (FR-032). Warnings list open sub-tasks when a task becomes done (FR-029).</summary>
    Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit, byte[] expectedVersion, CancellationToken ct);

    /// <summary>A sub-task in the leftmost "to do" column (FR-028, FR-040); returns the parent's details.</summary>
    Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title, CancellationToken ct);

    /// <summary>Moves a sub-task to the leftmost "done" column; returns the parent's details.</summary>
    Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion, CancellationToken ct);

    Task<Result<DeletePreview>> PreviewDeleteAsync(string workItemKey, CancellationToken ct);

    /// <summary>People with "Delete any work item", the creator with "Delete work items they created", and Admins (owner, 2026-10-26); sub-tasks are deleted with it.</summary>
    Task<Result> DeleteAsync(string workItemKey, CancellationToken ct);

    /// <summary>The space's archived (deleted) work items, as Jira's "Archived work items" view: View right.</summary>
    Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page, CancellationToken ct);

    /// <summary>Administrators only; restores the sub-tasks deleted with it.</summary>
    Task<Result> RestoreAsync(string workItemKey, CancellationToken ct);

    Task<Result<Page<SubtaskView>>> ListSubtasksAsync(string parentKey, PageRequest page, CancellationToken ct);

    /// <summary>The complete history in time order (FR-031).</summary>
    Task<Result<Page<ChangeView>>> GetHistoryAsync(string workItemKey, PageRequest page, CancellationToken ct);
}

/// <summary>One field saved from the drawer (FR-026).</summary>
public abstract record WorkItemEdit
{
    private WorkItemEdit()
    {
    }

    public sealed record Title(string Value) : WorkItemEdit;

    /// <summary>The description as the editor's JSON document, or null to clear it (<c>UnsupportedFormatting</c>,
    /// <c>InvalidLink</c>, <c>TextTooLong</c>) (Phase 4 FR-001, FR-002).</summary>
    public sealed record Description(string? Value) : WorkItemEdit;

    public sealed record Priority(Domain.Work.Priority Value) : WorkItemEdit;

    public sealed record Status(long ColumnId) : WorkItemEdit;

    /// <summary>Null unassigns; otherwise an active Project Admin or Member (<c>NotAssignable</c>) (Phase 2 FR-016, FR-017).</summary>
    public sealed record Assignee(Guid? UserId) : WorkItemEdit;

    /// <summary>Both dates in one edit; either may be null (<c>InvalidDates</c>) (Phase 2 FR-018, FR-019).</summary>
    public sealed record Dates(DateOnly? Start, DateOnly? Due) : WorkItemEdit;

    /// <summary>Story, Task or Bug in agile projects (<c>TypeChangeNotAllowed</c>) (Phase 3 FR-011).</summary>
    public sealed record Type(WorkItemType Value) : WorkItemEdit;

    /// <summary>The epic or stage by key, or null for none (<c>InvalidContainer</c>) (Phase 3 FR-015).</summary>
    public sealed record Container(string? ContainerKey) : WorkItemEdit;

    /// <summary>0 to 999.9 with one decimal, or null (validation) (Phase 3 FR-014).</summary>
    public sealed record Estimate(decimal? Value) : WorkItemEdit;

    /// <summary>An epic's color (<c>ColorNotAllowed</c> for other types) (Jira parity pass FR-025).</summary>
    public sealed record Color(EpicColor Value) : WorkItemEdit;
}

/// <summary>A new work item from the list, the backlog or a container's drawer (Phase 3 FR-010, FR-015), or with every field
/// from the Create form (Jira parity pass FR-004–FR-006). Each field follows the rule of its drawer edit.</summary>
/// <param name="ContainerKey">The epic or stage it starts in, if any.</param>
public sealed record NewWorkItem(WorkItemType Type, string Title, string? ContainerKey = null)
{
    /// <summary>The story, task or bug a sub-task belongs to; required for sub-tasks (<c>ParentRequired</c>,
    /// <c>InvalidParent</c>).</summary>
    public string? ParentKey { get; init; }

    /// <summary>The editor's JSON document.</summary>
    public string? Description { get; init; }

    /// <summary>One of the space's columns; the first "to do" column when null.</summary>
    public long? StatusId { get; init; }

    public Guid? AssigneeId { get; init; }

    /// <summary>Medium when null.</summary>
    public Priority? Priority { get; init; }

    public IReadOnlyList<string>? Labels { get; init; }

    /// <summary>An active or planned sprint of a Scrum space, for stories, tasks and bugs.</summary>
    public long? SprintId { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public decimal? Estimate { get; init; }
}

/// <summary>An epic or stage the drawer offers (Phase 3 FR-015).</summary>
/// <param name="IsLocked">A locked stage, which takes only items in a "to do" column (FR-033).</param>
public sealed record ContainerOption(string Key, string Title, WorkItemType Type, bool IsLocked);

/// <summary>The epic or stage an item belongs to.</summary>
/// <param name="Color">An epic's color for its tag; stages have none (Jira parity pass FR-025).</param>
public sealed record ContainerView(string Key, string Title, WorkItemType Type, EpicColor? Color = null);

/// <summary>A container's progress (Phase 3 FR-017): its standard items other than milestones that are done, out of all
/// of them; a container that is itself done shows 100%.</summary>
public sealed record Progress(int Done, int Total, bool ContainerDone = false)
{
    public int Percent => ContainerDone ? 100 : Total == 0 ? 0 : (int)Math.Round(100.0 * Done / Total, MidpointRounding.AwayFromZero);
}

/// <summary>A card's sprint in a Scrum project's drawer, read-only (Phase 3 FR-030). A card in no sprint, or open in a
/// completed one, is in the backlog.</summary>
public sealed record SprintView(int Number, string Name, SprintState State);

/// <summary>What an epic's or stage's drawer shows about its items (Phase 3 FR-016).</summary>
public sealed record ContainerSummary(Progress Progress, Page<ContainedItemView> Items);

public sealed record ContainedItemView(string Key, WorkItemType Type, string Title, StatusOption Status, AssigneeRef? Assignee,
    DateOnly? DueDate, byte[] Version);

/// <param name="Gate">For a stage, its gate (Phase 3 FR-031).</param>
/// <param name="LockedUntil">For work in a locked stage, the title of the stage whose gate must be approved before it can
/// start (Phase 3 FR-032).</param>
public sealed record WorkItemDetails(
    string Key,
    string ProjectKey,
    string ProjectName,
    WorkItemType Type,
    string Title,
    RichText? Description,
    Priority Priority,
    StatusOption Status,
    IReadOnlyList<StatusOption> Statuses,
    ParentRef? Parent,
    string CreatorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    bool CanDelete,
    byte[] Version,
    Page<SubtaskView> Subtasks,
    Page<CommentView> Comments,
    Page<ChangeView> History,
    AssigneeRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<AssigneeOption> AssigneeOptions,
    bool CanContribute = false,
    ProjectTemplate Template = ProjectTemplate.Kanban,
    IReadOnlyList<WorkItemType>? TypeChoices = null,
    ContainerView? Container = null,
    decimal? Estimate = null,
    ContainerSummary? AsContainer = null,
    SprintView? Sprint = null,
    GateSummary? Gate = null,
    string? LockedUntil = null,
    DependencyList? Dependencies = null,
    WatchersView? Watchers = null,
    IReadOnlyList<AttachmentView>? Attachments = null,
    IReadOnlyList<string>? Labels = null)
{
    /// <summary>The types the item can change into; empty when its type cannot change (Phase 3 FR-011).</summary>
    public IReadOnlyList<WorkItemType> TypeChoices { get; init; } = TypeChoices ?? [];

    /// <summary>The item's listed files (Phase 4 FR-026).</summary>
    public IReadOnlyList<AttachmentView> Attachments { get; init; } = Attachments ?? [];

    /// <summary>The item's labels, by name (Phase 4 FR-045).</summary>
    public IReadOnlyList<string> Labels { get; init; } = Labels ?? [];

    /// <summary>An epic's color (Jira parity pass FR-025); null for other types.</summary>
    public EpicColor? Color { get; init; }

    /// <summary>Jira's time tracking: the original and remaining estimates and the time logged, in minutes.</summary>
    public int? OriginalEstimateMinutes { get; init; }

    public int? RemainingEstimateMinutes { get; init; }

    public int TimeSpentMinutes { get; init; }

    /// <summary>People who voted for the item, and whether the viewer is one of them; why the viewer can't vote, if so.</summary>
    public int Votes { get; init; }

    public bool HasVoted { get; init; }

    public string? VoteRefusal { get; init; }

    /// <summary>The card's cover: a colour name or an image attachment.</summary>
    public string? CoverColor { get; init; }

    public long? CoverAttachmentId { get; init; }

    /// <summary>The viewer is a Space admin or an administrator (for "Configure").</summary>
    public bool CanManage { get; init; }

    /// <summary>The fields the item's type shows, in order (Jira's work type layout).</summary>
    public ItemFields Fields { get; init; } = ItemFields.Empty;

    /// <summary>The space fields on that layout, with their values.</summary>
    public IReadOnlyList<CustomFieldView> CustomFields { get; init; } = [];

    /// <summary>Stories, tasks and bugs of agile projects have an estimate (Phase 3 FR-014).</summary>
    public bool HasEstimate => WorkItemTypes.HasEstimate(Type, Template);
}

public sealed record StatusOption(long Id, string Name, StatusCategory Category);

public sealed record ParentRef(string Key, string Title);

public sealed record SubtaskView(string Key, string Title, StatusOption Status, Priority Priority, byte[] Version,
    AssigneeRef? Assignee, DateOnly? DueDate);

/// <summary>A task's assignee as cards, rows and the drawer show them (Phase 2 FR-021, FR-024).</summary>
/// <param name="CanWork">False once they left the project, became a Viewer or were deactivated.</param>
public sealed record AssigneeRef(Guid UserId, string DisplayName, string Initials, bool CanWork)
{
    public static AssigneeRef Of(Guid userId, string displayName, bool canWork) =>
        new(userId, displayName, InitialsOf(displayName), canWork);

    /// <summary>The first letters of the first and last words ("Amina Khan" → "AK"), or the first two letters of a
    /// single word.</summary>
    public static string InitialsOf(string displayName)
    {
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => $"{words[0][0]}{words[^1][0]}",
        };
        return initials.ToUpperInvariant();
    }
}

/// <summary>Someone who can be assigned: an active Project Admin or Member (Phase 2 FR-017).</summary>
public sealed record AssigneeOption(Guid UserId, string DisplayName, bool IsMe);

/// <param name="Body">Null for a deleted comment, which shows as "comment deleted".</param>
public sealed record CommentView(
    long Id,
    Guid AuthorId,
    string AuthorName,
    bool IsMine,
    RichText? Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    byte[] Version);

public sealed record ChangeView(
    DateTimeOffset OccurredAt,
    string ActorName,
    WorkItemField Field,
    string? OldValue,
    string? NewValue,
    string? Note);

public sealed record DeletePreview(string Key, string Title, int SubtaskCount);

public sealed record DeletedItemView(string Key, string Title, DateTimeOffset DeletedAt, string DeletedByName, int SubtaskCount)
{
    public WorkItemType Type { get; init; } = WorkItemType.Task;
}

/// <summary>A description or comment as stored (Phase 4 research R4): plain text from Phases 1–3, or a checked document's
/// JSON, with its plain text.</summary>
public sealed record RichText(TextFormat Format, string Content, string PlainText)
{
    public static RichText? Of(TextFormat format, string? content, string? plainText) =>
        content is null ? null : new RichText(format, content, plainText ?? content);
}
