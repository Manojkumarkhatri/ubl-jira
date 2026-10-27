using Upms.Domain.Common;

namespace Upms.Domain.Work;

public enum SprintState
{
    Planned,
    Active,
    Completed,
}

/// <summary>A time-box of a Scrum project (Phase 3 data-model.md "Sprint"; research R10): planned, then active, then
/// completed, and never changed again. Cards carry their sprint (<see cref="WorkItem.SprintId"/>); whether another
/// sprint of the project is active is the caller's rule, taken under the planning lock (FR-025).</summary>
public sealed class Sprint
{
    public const int NameMaxLength = 60;
    public const int GoalMaxLength = 500;
    public const string CompletedCode = "SprintCompleted";
    public const string AlreadyActiveCode = "SprintAlreadyActive";
    public const string NotActiveCode = "SprintNotActive";
    public const string NotPlannedCode = "SprintNotPlanned";

    /// <summary>A sprint ends two weeks after it starts unless another end is chosen (FR-025).</summary>
    public const int DefaultLengthDays = 14;

    private Sprint()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    /// <summary>1, 2, 3… per project.</summary>
    public int Number { get; private set; }

    public string Name { get; private set; } = "";

    public string? Goal { get; private set; }

    public SprintState State { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedById { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>The sprint as cards see it when they join or leave it.</summary>
    public SprintRef Ref => new(Id, Name, State == SprintState.Completed);

    /// <summary>A planned sprint named "<paramref name="projectKey"/> Sprint <paramref name="number"/>" (FR-023).</summary>
    public static Sprint Create(long projectId, string projectKey, int number, ChangeContext context) => new()
    {
        ProjectId = projectId,
        Number = number,
        Name = $"{projectKey} Sprint {number}",
        State = SprintState.Planned,
        CreatedAt = context.At,
        CreatedById = context.ActorId,
    };

    /// <summary>Renames a planned or active sprint and changes its goal; a blank goal clears it (FR-023).</summary>
    public DomainError? Update(string name, string? goal)
    {
        if (State == SprintState.Completed)
        {
            return Refused();
        }

        if ((ValidateName(name) ?? ValidateGoal(goal)) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        Goal = Normalize(goal);
        return null;
    }

    /// <summary>Starts a planned sprint. The end defaults to two weeks after the start; the goal stays when none is
    /// given (FR-025).</summary>
    public DomainError? Start(DateOnly start, DateOnly? end, string? goal, DateTimeOffset at)
    {
        switch (State)
        {
            case SprintState.Completed:
                return Refused();
            case SprintState.Active:
                return DomainError.Rule(NotPlannedCode, $"{Name} has already started.");
        }

        var endDate = end ?? start.AddDays(DefaultLengthDays);
        if ((ValidateDates(start, endDate) ?? ValidateGoal(goal)) is { } error)
        {
            return error;
        }

        State = SprintState.Active;
        StartDate = start;
        EndDate = endDate;
        StartedAt = at;
        Goal = Normalize(goal) ?? Goal;
        return null;
    }

    /// <summary>Completes the active sprint; where its open cards go is the caller's work (FR-028).</summary>
    public DomainError? Complete(DateTimeOffset at)
    {
        switch (State)
        {
            case SprintState.Completed:
                return Refused();
            case SprintState.Planned:
                return DomainError.Rule(NotActiveCode, $"{Name} has not started, so it cannot be completed.");
        }

        State = SprintState.Completed;
        CompletedAt = at;
        return null;
    }

    /// <summary>Only a planned sprint can be deleted; its cards return to the backlog (FR-023).</summary>
    public DomainError? CheckDeletable() => State switch
    {
        SprintState.Completed => Refused(),
        SprintState.Active => DomainError.Rule(NotPlannedCode, $"{Name} has started. Complete it instead of deleting it."),
        _ => null,
    };

    /// <summary>An active sprint past its end date stays active, marked as overdue (spec edge case).</summary>
    public bool IsOverdue(DateOnly today) => State == SprintState.Active && EndDate < today;

    private DomainError Refused() => DomainError.Rule(CompletedCode, $"{Name} is completed and can no longer change.");

    private static DomainError? ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed.Length switch
        {
            0 => DomainError.Invalid("Name", "Enter a name for the sprint."),
            > NameMaxLength => DomainError.Invalid("Name", $"A sprint's name can have at most {NameMaxLength} characters."),
            _ => null,
        };
    }

    private static DomainError? ValidateGoal(string? goal) => Normalize(goal) is { Length: > GoalMaxLength }
        ? DomainError.Invalid("Goal", $"A sprint's goal can have at most {GoalMaxLength} characters.")
        : null;

    private static DomainError? ValidateDates(DateOnly start, DateOnly end)
    {
        const string outOfRange = "Choose a date between 1 Jan 2000 and 31 Dec 2099.";
        if (start < WorkItem.EarliestDate || start > WorkItem.LatestDate)
        {
            return new DomainError(WorkItem.InvalidDatesCode, outOfRange, "StartDate");
        }

        if (end < WorkItem.EarliestDate || end > WorkItem.LatestDate)
        {
            return new DomainError(WorkItem.InvalidDatesCode, outOfRange, "EndDate");
        }

        return end < start ? new DomainError(WorkItem.InvalidDatesCode, "The end date cannot be before the start date.", "EndDate") : null;
    }

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
