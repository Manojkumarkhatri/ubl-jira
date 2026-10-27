using System.Globalization;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A work item of any type (data-model.md, "WorkItem"; research R12; Phase 3 data-model.md). It changes only
/// through its methods, each of which appends history entries in the same unit of work (research R17).</summary>
public sealed class WorkItem
{
    public const int TitleMaxLength = 255;
    public const int DescriptionMaxLength = 32_000;
    public const int KeyMaxLength = 21;
    public const string InvalidDatesCode = "InvalidDates";
    public const string TypeNotAllowedCode = "TypeNotAllowed";
    public const string TypeChangeNotAllowedCode = "TypeChangeNotAllowed";
    public const string InvalidContainerCode = "InvalidContainer";
    public const string NoSubtasksCode = "NoSubtasks";
    public const string ColorNotAllowedCode = "ColorNotAllowed";

    /// <summary>The largest estimate (Phase 3 FR-014); estimates have at most one decimal place.</summary>
    public const decimal MaxEstimate = 999.9m;

    /// <summary>The first and last dates a task can have (Phase 2 FR-018).</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    public static readonly DateOnly LatestDate = new(2099, 12, 31);

    private readonly List<WorkItemChange> _changes = [];

    private WorkItem()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public int Number { get; private set; }

    /// <summary>Unique and immutable, for example <c>WEB-42</c> (FR-024).</summary>
    public string Key { get; private set; } = "";

    public WorkItemType Type { get; private set; }

    public long? ParentId { get; private set; }

    public string Title { get; private set; } = "";

    /// <summary>Plain text, or the document's JSON when <see cref="DescriptionFormat"/> is
    /// <see cref="TextFormat.Document"/> (Phase 4 research R4).</summary>
    public string? Description { get; private set; }

    public TextFormat DescriptionFormat { get; private set; }

    /// <summary>The description's plain text, for the List view's word filter, history and search.</summary>
    public string? DescriptionText { get; private set; }

    public Priority Priority { get; private set; } = Priority.Medium;

    public long StatusId { get; private set; }

    /// <summary>Fractional index for the order within its column (research R14).</summary>
    public string Rank { get; private set; } = "";

    public Guid CreatedById { get; private set; }

    /// <summary>Who does the work: an active Project Admin or Member when assigned; kept if they later leave the
    /// project, become a Viewer or are deactivated (Phase 2 FR-016, FR-024).</summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>Calendar dates, the same for every viewer (Phase 2 FR-018, FR-042).</summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>The epic or stage a standard item belongs to (Phase 3 FR-008, FR-015). Separate from
    /// <see cref="ParentId"/>, which only sub-tasks have, because a top-level item is a card on the board (research R5).</summary>
    public long? ContainerId { get; private set; }

    /// <summary>Story points or any unit the team chooses, 0–999.9 with one decimal; stories, tasks and bugs of agile
    /// projects only (Phase 3 FR-014).</summary>
    public decimal? Estimate { get; private set; }

    /// <summary>The sprint of a card in a Scrum project (Phase 3 FR-024). Sub-tasks are in their parent's sprint and
    /// have none of their own. An open card whose sprint is completed is in the backlog (research R10).</summary>
    public long? SprintId { get; private set; }

    /// <summary>An epic's color for its tags; other types have none (Jira parity pass FR-025).</summary>
    public EpicColor? Color { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When the item entered a "done" status; cleared when it leaves (FR-027).</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Jira's time tracking: the original estimate, in minutes.</summary>
    public int? OriginalEstimateMinutes { get; private set; }

    /// <summary>The time still to spend, in minutes; it goes down as work is logged.</summary>
    public int? RemainingEstimateMinutes { get; private set; }

    /// <summary>All the time logged on the item, in minutes.</summary>
    public int TimeSpentMinutes { get; private set; }

    /// <summary>The card's cover colour on the board, by name, as Jira's "Select cover".</summary>
    public string? CoverColor { get; private set; }

    /// <summary>An image attachment shown as the card's cover.</summary>
    public long? CoverAttachmentId { get; private set; }

    /// <summary>History entries added in the current unit of work.</summary>
    public IReadOnlyCollection<WorkItemChange> Changes => _changes;

    /// <summary>A new task (FR-018, FR-024, FR-025).</summary>
    public static DomainResult<WorkItem> CreateTask(long projectId, string projectKey, int number, string title,
        StatusRef status, string rank, ChangeContext context) =>
        Create(projectId, projectKey, number, WorkItemType.Task, title, status, rank, ProjectTemplate.Kanban, context);

    /// <summary>A new work item of a type the space offers: its template's, among those it has turned on (Phase 3 FR-007,
    /// FR-010; Jira parity pass FR-017); sub-tasks are made with <see cref="AddSubtask"/>.</summary>
    public static DomainResult<WorkItem> Create(long projectId, string projectKey, int number, WorkItemType type, string title,
        StatusRef status, string rank, ProjectTemplate template, ChangeContext context, WorkTypeSet workTypes = WorkTypeSet.All)
    {
        if (type == WorkItemType.Subtask)
        {
            return DomainError.Rule(TypeNotAllowedCode, "Sub-tasks are added from the item they belong to.");
        }

        if (WorkItemTypes.RefusalFor(type, template, workTypes) is { } refusal)
        {
            return DomainError.Rule(TypeNotAllowedCode, refusal);
        }

        if (ValidateTitle(title) is { } error)
        {
            return error;
        }

        var item = new WorkItem
        {
            ProjectId = projectId,
            Number = number,
            Key = $"{projectKey}-{number}",
            Type = type,
            Title = title.Trim(),
            StatusId = status.Id,
            Rank = rank,
            CreatedById = context.ActorId,
            CreatedAt = context.At,
            UpdatedAt = context.At,
            ResolvedAt = status.IsDone ? context.At : null,
            Color = type == WorkItemType.Epic ? EpicColors.ForNumber(number) : null,
        };
        item.Record(WorkItemField.Created, null, status.Name, null, context);
        return item;
    }

    /// <summary>Changes an epic's color (Jira parity pass FR-025); other types have none.</summary>
    public DomainError? SetColor(EpicColor color, ChangeContext context)
    {
        if (Type != WorkItemType.Epic)
        {
            return DomainError.Rule(ColorNotAllowedCode, "Only epics have a color.");
        }

        if (color != Color)
        {
            Record(WorkItemField.Color, Color?.ToString(), color.ToString(), null, context);
            Color = color;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Moves the item to another status (recorded as <c>Status</c>) or to another position in
    /// its status (recorded as <c>Rank</c> with the old and new 1-based positions) (FR-019, FR-027, FR-031).</summary>
    public void Move(StatusRef current, StatusRef target, string newRank, int? oldPosition, int? newPosition,
        string? note, ChangeContext context)
    {
        if (current.Id != StatusId)
        {
            throw new InvalidOperationException($"{Key} is not in status {current.Name}.");
        }

        if (target.Id != StatusId)
        {
            StatusId = target.Id;
            if (target.IsDone && !current.IsDone)
            {
                ResolvedAt = context.At;
            }
            else if (!target.IsDone)
            {
                ResolvedAt = null;
            }

            Record(WorkItemField.Status, current.Name, target.Name, note, context);
        }
        else if (newRank != Rank)
        {
            Record(WorkItemField.Rank, oldPosition?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                newPosition?.ToString(System.Globalization.CultureInfo.InvariantCulture), note, context);
        }

        Rank = newRank;
        UpdatedAt = context.At;
    }

    /// <summary>Assigns a new rank without changing the order (rebalancing); not a change to the item.</summary>
    public void Rerank(string rank) => Rank = rank;

    /// <summary>Changes the title (FR-026); unchanged titles record nothing.</summary>
    public DomainError? Rename(string title, ChangeContext context)
    {
        if (ValidateTitle(title) is { } error)
        {
            return error;
        }

        var trimmed = title.Trim();
        if (trimmed != Title)
        {
            Record(WorkItemField.Title, Title, trimmed, null, context);
            Title = trimmed;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Changes the plain-text description; blank clears it (FR-025, FR-026).</summary>
    public DomainError? Describe(string? description, ChangeContext context)
    {
        var value = string.IsNullOrWhiteSpace(description) ? null : description;
        if (value is { Length: > DescriptionMaxLength })
        {
            return DomainError.Invalid("Description", $"The description can have at most {DescriptionMaxLength:N0} characters.");
        }

        if (value != Description || DescriptionFormat != TextFormat.Plain)
        {
            Record(WorkItemField.Description, DescriptionText, value, null, context);
            Description = DescriptionText = value;
            DescriptionFormat = TextFormat.Plain;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Changes the description to formatted text (Phase 4 FR-001); an empty document clears it. History keeps
    /// the old and new plain text, as before (research R4).</summary>
    public DomainError? Describe(FormattedText? description, ChangeContext context)
    {
        if (description is null || description.IsEmpty)
        {
            return Describe((string?)null, context);
        }

        if (DescriptionFormat != TextFormat.Document || description.Json != Description)
        {
            Record(WorkItemField.Description, DescriptionText, description.PlainText, null, context);
            Description = description.Json;
            DescriptionFormat = TextFormat.Document;
            DescriptionText = description.PlainText;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Changes the priority (FR-026).</summary>
    public void Prioritize(Priority priority, ChangeContext context)
    {
        if (priority != Priority)
        {
            Record(WorkItemField.Priority, Priority.ToString(), priority.ToString(), null, context);
            Priority = priority;
            UpdatedAt = context.At;
        }
    }

    /// <summary>Sets, changes or clears the assignee (Phase 2 FR-016, FR-023). <paramref name="current"/> names the
    /// present assignee for the history; whether <paramref name="assignee"/> may be assigned is the team's rule,
    /// checked by the caller.</summary>
    public void Assign(PersonRef? current, PersonRef? assignee, ChangeContext context)
    {
        if (current?.Id != AssigneeId)
        {
            throw new InvalidOperationException($"{Key} is not assigned to {current?.DisplayName ?? "nobody"}.");
        }

        if (assignee?.Id != AssigneeId)
        {
            Record(WorkItemField.Assignee, current?.DisplayName, assignee?.DisplayName, null, context);
            AssigneeId = assignee?.Id;
            UpdatedAt = context.At;
        }
    }

    /// <summary>Sets, changes or clears both dates as one edit (Phase 2 FR-018, FR-023): each changed date is
    /// recorded with ISO values in the same change set. A milestone has only its due date (Phase 3 FR-012).</summary>
    public DomainError? Schedule(DateOnly? start, DateOnly? due, ChangeContext context)
    {
        if (Type == WorkItemType.Milestone && start is not null)
        {
            return new DomainError(InvalidDatesCode, "A milestone has a single date: its due date.", "StartDate");
        }

        if (ValidateDates(start, due) is { } error)
        {
            return error;
        }

        if (start != StartDate)
        {
            Record(WorkItemField.StartDate, Iso(StartDate), Iso(start), null, context);
            StartDate = start;
            UpdatedAt = context.At;
        }

        if (due != DueDate)
        {
            Record(WorkItemField.DueDate, Iso(DueDate), Iso(due), null, context);
            DueDate = due;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Either date may be missing; both lie in 2000–2099 and the due date is not before the start date.</summary>
    public static DomainError? ValidateDates(DateOnly? start, DateOnly? due)
    {
        const string outOfRange = "Choose a date between 1 Jan 2000 and 31 Dec 2099.";
        if (start is { } s && (s < EarliestDate || s > LatestDate))
        {
            return new DomainError(InvalidDatesCode, outOfRange, "StartDate");
        }

        if (due is { } d && (d < EarliestDate || d > LatestDate))
        {
            return new DomainError(InvalidDatesCode, outOfRange, "DueDate");
        }

        return start > due ? new DomainError(InvalidDatesCode, "The due date cannot be before the start date.", "DueDate") : null;
    }

    /// <summary>Changes the type among Story, Task and Bug in agile projects (Phase 3 FR-011); anything else is
    /// refused.</summary>
    public DomainError? ChangeType(WorkItemType type, ProjectTemplate template, ChangeContext context)
    {
        if (type == Type)
        {
            return null;
        }

        if (!WorkItemTypes.CanChange(Type, type, template))
        {
            return DomainError.Rule(TypeChangeNotAllowedCode,
                "Only stories, tasks and bugs of agile spaces can change type; create a new item instead.");
        }

        Record(WorkItemField.Type, Type.ToString(), type.ToString(), null, context);
        Type = type;
        UpdatedAt = context.At;
        return null;
    }

    /// <summary>Puts a standard item in an epic or stage of its project, moves it to another one, or takes it out with
    /// null (Phase 3 FR-008, FR-015). <paramref name="current"/> names the present container for the history; whether
    /// a locked stage may take the item is the caller's rule (FR-033).</summary>
    public DomainError? PlaceIn(ContainerRef? current, ContainerRef? target, ChangeContext context)
    {
        if (current?.Id != ContainerId)
        {
            throw new InvalidOperationException($"{Key} is not in {current?.Key ?? "no container"}.");
        }

        if (target?.Id == ContainerId)
        {
            return null;
        }

        if (target is not null && (WorkItemTypes.LevelOf(Type) != WorkItemLevel.Standard
            || WorkItemTypes.LevelOf(target.Type) != WorkItemLevel.Container || target.ProjectId != ProjectId))
        {
            return DomainError.Rule(InvalidContainerCode,
                "Only a story, task, bug or milestone can go into an epic or stage of the same space.");
        }

        Record(WorkItemField.Container, current?.Label, target?.Label, null, context);
        ContainerId = target?.Id;
        UpdatedAt = context.At;
        return null;
    }

    /// <summary>Sets or clears the estimate of a story, task or bug in an agile project (Phase 3 FR-014).</summary>
    public DomainError? SetEstimate(decimal? estimate, ProjectTemplate template, ChangeContext context)
    {
        if (!WorkItemTypes.HasEstimate(Type, template))
        {
            return DomainError.Invalid("Estimate", "Only stories, tasks and bugs of agile spaces have an estimate.");
        }

        if (estimate is { } value && (value < 0 || value > MaxEstimate || decimal.Round(value, 1) != value))
        {
            return DomainError.Invalid("Estimate", "An estimate is a number from 0 to 999.9, with at most one decimal place.");
        }

        if (estimate != Estimate)
        {
            Record(WorkItemField.Estimate, FormatEstimate(Estimate), FormatEstimate(estimate), null, context);
            Estimate = estimate;
            UpdatedAt = context.At;
        }

        return null;
    }

    /// <summary>Moves a card into a sprint, to another sprint, or to the backlog with null (Phase 3 FR-024, FR-030).
    /// <paramref name="current"/> names the card's present sprint; an open card whose sprint is completed is in the
    /// backlog, while a done one stays in its completed sprint (FR-028). Nothing joins a completed sprint.</summary>
    public DomainError? JoinSprint(SprintRef? current, SprintRef? target, ChangeContext context)
    {
        if (current?.Id != SprintId)
        {
            throw new InvalidOperationException($"{Key} is not in {current?.Name ?? "the backlog"}.");
        }

        if (ParentId is not null || !WorkItemTypes.IsCard(Type))
        {
            return DomainError.Rule(TypeNotAllowedCode, "Only stories, tasks and bugs go into sprints; sub-tasks follow their parent.");
        }

        if (target is { IsCompleted: true })
        {
            return DomainError.Rule(Sprint.CompletedCode, $"{target.Name} is completed and can no longer change.");
        }

        if (current is { IsCompleted: true } && ResolvedAt is not null)
        {
            return DomainError.Rule(Sprint.CompletedCode, $"{Key} was done in {current.Name}, which is completed, so it stays there.");
        }

        var from = current is { IsCompleted: true } ? null : current;
        if (target?.Id == from?.Id)
        {
            return null;
        }

        Record(WorkItemField.Sprint, from?.Name, target?.Name, null, context);
        SprintId = target?.Id;
        UpdatedAt = context.At;
        return null;
    }

    /// <summary>"3" or "2.5", as history and screens show estimates.</summary>
    public static string? FormatEstimate(decimal? estimate) =>
        estimate?.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>A sub-task of this story, task or bug with its own key (FR-028). Sub-tasks cannot have sub-tasks, and
    /// milestones, epics and stages have none (Phase 3 FR-008, FR-012).</summary>
    public DomainResult<WorkItem> AddSubtask(int number, string title, StatusRef status, string rank, ChangeContext context)
    {
        if (Type == WorkItemType.Subtask)
        {
            return DomainError.Rule(SubtaskDepthCode, "A sub-task cannot have sub-tasks of its own.");
        }

        if (!WorkItemTypes.IsCard(Type))
        {
            return DomainError.Rule(NoSubtasksCode, $"{WorkItemTypes.DisplayName(Type)}s have no sub-tasks.");
        }

        var created = CreateTask(ProjectId, ProjectKey, number, title, status, rank, context);
        if (!created.IsSuccess)
        {
            return created;
        }

        var subtask = created.Value!;
        subtask.Type = WorkItemType.Subtask;
        subtask.ParentId = Id;
        RecordActivity(WorkItemField.SubtaskAdded, null, subtask.Key, context, subtask.Title);
        return subtask;
    }

    /// <summary>Soft-deletes the item with its sub-tasks in one change set (FR-033).</summary>
    public void Delete(IEnumerable<WorkItem> subtasks, ChangeContext context)
    {
        foreach (var item in subtasks.Where(s => !s.IsDeleted).Prepend(this))
        {
            item.IsDeleted = true;
            item.DeletedAt = context.At;
            item.DeletedById = context.ActorId;
            item.Record(WorkItemField.Deleted, null, null, null, context);
        }
    }

    /// <summary>Restores the item and the sub-tasks that were deleted with it (FR-033).</summary>
    public void Restore(IEnumerable<WorkItem> subtasks, ChangeContext context)
    {
        var deletedAt = DeletedAt;
        foreach (var item in subtasks.Where(s => s.IsDeleted && s.DeletedAt == deletedAt).Prepend(this))
        {
            item.IsDeleted = false;
            item.DeletedAt = null;
            item.DeletedById = null;
            item.Record(WorkItemField.Restored, null, null, null, context);
        }
    }

    /// <summary>Sets or clears the original estimate. Until time is logged, or while nothing remains set, the remaining
    /// estimate follows it less the time logged, as in Jira.</summary>
    public void SetOriginalEstimate(int? minutes, ChangeContext context)
    {
        if (minutes == OriginalEstimateMinutes)
        {
            return;
        }

        Record(WorkItemField.OriginalEstimate, WorkDuration.FormatOrNull(OriginalEstimateMinutes), WorkDuration.FormatOrNull(minutes), null, context);
        OriginalEstimateMinutes = minutes;
        if (TimeSpentMinutes == 0 || RemainingEstimateMinutes is null)
        {
            RemainingEstimateMinutes = minutes is { } estimate ? Math.Max(0, estimate - TimeSpentMinutes) : null;
        }

        UpdatedAt = context.At;
    }

    public void SetRemainingEstimate(int? minutes, ChangeContext context)
    {
        if (minutes == RemainingEstimateMinutes)
        {
            return;
        }

        Record(WorkItemField.RemainingEstimate, WorkDuration.FormatOrNull(RemainingEstimateMinutes), WorkDuration.FormatOrNull(minutes), null, context);
        RemainingEstimateMinutes = minutes;
        UpdatedAt = context.At;
    }

    /// <summary>Adds logged time. The remaining estimate becomes <paramref name="remaining"/> when given, otherwise it goes
    /// down by the time logged (never below zero), as Jira's "Adjust automatically".</summary>
    public void LogWork(int minutes, int? remaining, ChangeContext context)
    {
        TimeSpentMinutes += minutes;
        var estimate = RemainingEstimateMinutes ?? OriginalEstimateMinutes;
        RemainingEstimateMinutes = remaining ?? (estimate is { } left ? Math.Max(0, left - minutes) : null);
        Record(WorkItemField.WorkLogged, null, WorkDuration.Format(minutes), null, context);
        UpdatedAt = context.At;
    }

    public void ChangeLoggedWork(int oldMinutes, int newMinutes, ChangeContext context)
    {
        if (oldMinutes == newMinutes)
        {
            return;
        }

        TimeSpentMinutes = Math.Max(0, TimeSpentMinutes - oldMinutes + newMinutes);
        if (RemainingEstimateMinutes is { } left)
        {
            RemainingEstimateMinutes = Math.Max(0, left + oldMinutes - newMinutes);
        }

        Record(WorkItemField.WorkLogChanged, WorkDuration.Format(oldMinutes), WorkDuration.Format(newMinutes), null, context);
        UpdatedAt = context.At;
    }

    /// <summary>Takes a deleted log's time off, and gives it back to the remaining estimate, as Jira does by default.</summary>
    public void RemoveLoggedWork(int minutes, ChangeContext context)
    {
        TimeSpentMinutes = Math.Max(0, TimeSpentMinutes - minutes);
        if (RemainingEstimateMinutes is { } left)
        {
            RemainingEstimateMinutes = left + minutes;
        }

        Record(WorkItemField.WorkLogDeleted, WorkDuration.Format(minutes), null, null, context);
        UpdatedAt = context.At;
    }

    /// <summary>The description of another item, as it is, for a clone (Jira's "Clone").</summary>
    public void CopyDescription(WorkItem source)
    {
        Description = source.Description;
        DescriptionFormat = source.DescriptionFormat;
        DescriptionText = source.DescriptionText;
    }

    /// <summary>A space field's value changed: the history names the field and shows both values in words.</summary>
    public void FieldChanged(string fieldName, string? oldValue, string? newValue, ChangeContext context)
    {
        Record(WorkItemField.CustomField, oldValue, newValue, fieldName, context);
        UpdatedAt = context.At;
    }

    /// <summary>The card's cover: a colour, an image attachment, or neither. Not a change to the work itself.</summary>
    public void SetCover(string? color, long? attachmentId)
    {
        CoverColor = color;
        CoverAttachmentId = attachmentId;
    }

    /// <summary>Changes the type and parent in a move (Jira's "Move"): any of epic, story, task, bug and sub-task. The
    /// caller has already taken the item out of its epic and sprint where the new type cannot keep them.</summary>
    public void Convert(WorkItemType type, long? parentId, string? oldParentKey, string? newParentKey, ChangeContext context)
    {
        if (type != Type)
        {
            Record(WorkItemField.Type, Type.ToString(), type.ToString(), "moved", context);
            Type = type;
        }

        if (parentId != ParentId)
        {
            Record(WorkItemField.Parent, oldParentKey, newParentKey, null, context);
            ParentId = parentId;
        }

        Color = type == WorkItemType.Epic ? Color ?? EpicColors.ForNumber(Number) : null;
        UpdatedAt = context.At;
    }

    /// <summary>Moves the item to another space: a new number and key there, a status of that space and a place in its
    /// order. The old key is kept in the history.</summary>
    public void Relocate(long projectId, string projectKey, int number, StatusRef current, StatusRef status, string rank, ChangeContext context)
    {
        var oldKey = Key;
        ProjectId = projectId;
        Number = number;
        Key = $"{projectKey}-{number}";
        if (status.Id != StatusId)
        {
            // Each space has its own columns: a column of the same name is no change worth a line in the history.
            if (!string.Equals(current.Name, status.Name, StringComparison.OrdinalIgnoreCase))
            {
                Record(WorkItemField.Status, current.Name, status.Name, "moved", context);
            }

            StatusId = status.Id;
            ResolvedAt = status.IsDone ? ResolvedAt ?? context.At : null;
        }

        Rank = rank;
        Record(WorkItemField.Moved, oldKey, Key, null, context);
        UpdatedAt = context.At;
    }

    /// <summary>Records activity on the item (comments, sub-tasks added) without changing the item itself, so
    /// that people editing the task are not told it changed (FR-031, FR-032).</summary>
    public void RecordActivity(WorkItemField field, string? oldValue, string? newValue, ChangeContext context, string? note = null) =>
        Record(field, oldValue, newValue, note, context);

    public const string SubtaskDepthCode = "SubtaskDepth";

    private string ProjectKey => Key[..Key.LastIndexOf('-')];

    public static DomainError? ValidateTitle(string? title)
    {
        var trimmed = title?.Trim() ?? "";
        return trimmed.Length switch
        {
            0 => DomainError.Invalid("Title", "Enter a title."),
            > TitleMaxLength => DomainError.Invalid("Title", $"The title can have at most {TitleMaxLength} characters."),
            _ => null,
        };
    }

    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private void Record(WorkItemField field, string? oldValue, string? newValue, string? note, ChangeContext context) =>
        _changes.Add(new WorkItemChange(field, oldValue, newValue, note, context));
}
