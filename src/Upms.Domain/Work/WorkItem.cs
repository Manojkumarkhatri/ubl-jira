using System.Globalization;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A task or sub-task (data-model.md, "WorkItem"; research R12). It changes only through its
/// methods, each of which appends history entries in the same unit of work (research R17).</summary>
public sealed class WorkItem
{
    public const int TitleMaxLength = 255;
    public const int DescriptionMaxLength = 32_000;
    public const int KeyMaxLength = 21;
    public const string InvalidDatesCode = "InvalidDates";

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

    public string? Description { get; private set; }

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

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When the item entered a "done" status; cleared when it leaves (FR-027).</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>History entries added in the current unit of work.</summary>
    public IReadOnlyCollection<WorkItemChange> Changes => _changes;

    /// <summary>A new task (FR-018, FR-024, FR-025).</summary>
    public static DomainResult<WorkItem> CreateTask(long projectId, string projectKey, int number, string title,
        StatusRef status, string rank, ChangeContext context)
    {
        if (ValidateTitle(title) is { } error)
        {
            return error;
        }

        var item = new WorkItem
        {
            ProjectId = projectId,
            Number = number,
            Key = $"{projectKey}-{number}",
            Type = WorkItemType.Task,
            Title = title.Trim(),
            StatusId = status.Id,
            Rank = rank,
            CreatedById = context.ActorId,
            CreatedAt = context.At,
            UpdatedAt = context.At,
            ResolvedAt = status.IsDone ? context.At : null,
        };
        item.Record(WorkItemField.Created, null, status.Name, null, context);
        return item;
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

        if (value != Description)
        {
            Record(WorkItemField.Description, Description, value, null, context);
            Description = value;
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
    /// recorded with ISO values in the same change set.</summary>
    public DomainError? Schedule(DateOnly? start, DateOnly? due, ChangeContext context)
    {
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

    /// <summary>A sub-task of this task with its own key (FR-028). Sub-tasks cannot have sub-tasks.</summary>
    public DomainResult<WorkItem> AddSubtask(int number, string title, StatusRef status, string rank, ChangeContext context)
    {
        if (Type != WorkItemType.Task)
        {
            return DomainError.Rule(SubtaskDepthCode, "A sub-task cannot have sub-tasks of its own.");
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
