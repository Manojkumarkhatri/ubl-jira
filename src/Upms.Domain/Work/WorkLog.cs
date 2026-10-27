using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>Time someone spent on a work item, as Jira's "Log work": how long, when they started, and what they did.</summary>
public sealed class WorkLog
{
    public const int NoteMaxLength = 2000;

    private WorkLog()
    {
    }

    public long Id { get; private set; }

    public long WorkItemId { get; private set; }

    public Guid AuthorId { get; private set; }

    /// <summary>When the work started, as the person entered it.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    public int Minutes { get; private set; }

    /// <summary>Plain text: what was done.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DomainResult<WorkLog> Create(long workItemId, Guid authorId, DateTimeOffset startedAt, int minutes, string? note, DateTimeOffset now)
    {
        var log = new WorkLog { WorkItemId = workItemId, AuthorId = authorId, CreatedAt = now };
        if (log.Change(startedAt, minutes, note, now) is { } error)
        {
            return error;
        }

        return log;
    }

    public DomainError? Change(DateTimeOffset startedAt, int minutes, string? note, DateTimeOffset now)
    {
        if (minutes <= 0 || minutes > WorkDuration.MaxMinutes)
        {
            return DomainError.Invalid("Minutes", "Enter the time spent, such as 2h 30m.");
        }

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > NoteMaxLength })
        {
            return DomainError.Invalid("Note", $"The description can have at most {NoteMaxLength:N0} characters.");
        }

        StartedAt = startedAt;
        Minutes = minutes;
        Note = text;
        UpdatedAt = now;
        return null;
    }
}

/// <summary>A person's vote for a work item, as Jira's "Add vote": more votes mean it matters to more people.</summary>
public sealed class WorkItemVote
{
    private WorkItemVote()
    {
    }

    public long WorkItemId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkItemVote Create(long workItemId, Guid userId, DateTimeOffset now) =>
        new() { WorkItemId = workItemId, UserId = userId, CreatedAt = now };
}
