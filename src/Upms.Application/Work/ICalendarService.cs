using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A space's Calendar view, as Jira's: work items on their due date (or their start date when they have no due
/// date), a month at a time.</summary>
public interface ICalendarService
{
    Task<Result<CalendarView>> GetAsync(string spaceKey, DateOnly from, DateOnly to, CancellationToken ct);
}

/// <param name="Items">At most <see cref="MaxItems"/>, by date.</param>
/// <param name="UnscheduledCount">Live items with no dates at all.</param>
public sealed record CalendarView(string SpaceKey, string SpaceName, ProjectTemplate Template, bool CanManage, bool CanContribute,
    bool CanRestoreDeleted, IReadOnlyList<CalendarItem> Items, int UnscheduledCount)
{
    public const int MaxItems = 500;
}

/// <param name="Date">The day the calendar shows it on: its due date, or its start date when it has no due date.</param>
/// <param name="Version">For moving it to another day (<see cref="ITimelineService.RescheduleAsync"/>).</param>
public sealed record CalendarItem(string Key, string Title, WorkItemType Type, StatusCategory Category, string Status, DateOnly Date,
    DateOnly? StartDate, DateOnly? DueDate, AssigneeRef? Assignee, byte[] Version);
