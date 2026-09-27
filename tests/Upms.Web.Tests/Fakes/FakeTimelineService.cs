using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;

namespace Upms.Web.Tests.Fakes;

/// <summary>WEB's timeline: WEB-1 (5–10 Oct, in progress, Bilal), WEB-2 (due 3 Oct), WEB-5 (no dates, with sub-task
/// WEB-7 from 1 Oct and undated sub-task WEB-8), and WEB-4 unscheduled. Records reschedules.</summary>
public sealed class FakeTimelineService : ITimelineService
{
    public static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    public static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);
    public static readonly StatusOption Done = new(3, "Done", StatusCategory.Done);
    public static readonly Guid Bilal = Guid.NewGuid();

    private static DateOnly Oct(int day) => new(2026, 10, day);

    public List<TimelineRow> Rows { get; set; } =
    [
        new(Item("WEB-7", "Record the video", ToDo, Oct(1), null) with { Key = "WEB-5", Title = "Prepare the demo", StartDate = null },
            [Item("WEB-7", "Record the video", ToDo, Oct(1), null)], [Item("WEB-8", "Choose a date", ToDo, null, null)]),
        new(Item("WEB-2", "Book the venue", ToDo, null, Oct(3)), [], []),
        new(Item("WEB-1", "Plan the launch", InProgress, Oct(5), Oct(10), AssigneeRef.Of(Bilal, "Bilal Ahmed", canWork: true)), [], []),
    ];

    public List<TimelineItem> Unscheduled { get; } = [Item("WEB-4", "Someday", ToDo, null, null)];

    public bool CanContribute { get; set; } = true;

    public List<bool> Loads { get; } = [];

    public List<(string Key, DateOnly? Start, DateOnly? Due, byte[] Version)> Reschedules { get; } = [];

    /// <summary>Returned instead of saving the next reschedule.</summary>
    public Func<Result<TimelineItem>>? NextResult { get; set; }

    public static TimelineItem Item(string key, string title, StatusOption status, DateOnly? start, DateOnly? due, AssigneeRef? assignee = null) =>
        new(key, title, status, assignee, start, due, status.Category != StatusCategory.Done, [1, 2, 3]);

    public Task<Result<TimelineView>> GetAsync(string projectKey, bool hideCompleted, CancellationToken ct)
    {
        Loads.Add(hideCompleted);
        if (projectKey != "WEB")
        {
            return Task.FromResult(Result<TimelineView>.Fail(AppError.NotFound("project")));
        }

        return Task.FromResult(Result<TimelineView>.Ok(new TimelineView("WEB", "Website Revamp", CanContribute, false, false, Rows,
            Rows.Count, new Page<TimelineItem>(Unscheduled, Unscheduled.Count, 1, 50), hideCompleted)));
    }

    public Task<Result<Page<TimelineItem>>> ListUnscheduledAsync(string projectKey, bool hideCompleted, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<TimelineItem>>.Ok(new Page<TimelineItem>([], Unscheduled.Count, page.Page, 50)));

    public Task<Result<TimelineItem>> RescheduleAsync(string workItemKey, DateOnly? start, DateOnly? due, byte[] expectedVersion,
        CancellationToken ct)
    {
        Reschedules.Add((workItemKey, start, due, expectedVersion));
        if (NextResult is { } next)
        {
            NextResult = null;
            return Task.FromResult(next());
        }

        var unscheduled = Unscheduled.FirstOrDefault(i => i.Key == workItemKey);
        if (unscheduled is not null)
        {
            Unscheduled.Remove(unscheduled);
            Rows.Add(new TimelineRow(unscheduled with { StartDate = start, DueDate = due }, [], []));
            return Task.FromResult(Result<TimelineItem>.Ok(unscheduled with { StartDate = start, DueDate = due }));
        }

        Rows = Rows.ConvertAll(r => r.Task.Key == workItemKey
            ? r with { Task = r.Task with { StartDate = start, DueDate = due, Version = [9] } }
            : r with { ScheduledSubtasks = r.ScheduledSubtasks.Select(s => s.Key == workItemKey ? s with { StartDate = start, DueDate = due } : s).ToList() });
        var saved = Rows.SelectMany(r => r.ScheduledSubtasks.Prepend(r.Task)).Single(i => i.Key == workItemKey);
        return Task.FromResult(Result<TimelineItem>.Ok(saved));
    }
}
