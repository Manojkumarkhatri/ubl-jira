using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Work;

internal sealed class CalendarService(IAppDbContext db, IProjectAccess access, IProjectWorkflow workflow, AssigneeReads assignees)
    : ICalendarService
{
    public async Task<Result<CalendarView>> GetAsync(string spaceKey, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var items = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == caller.ProjectId); // live items only
        var rows = await items
            .Where(w => (w.DueDate != null && w.DueDate >= from && w.DueDate <= to)
                || (w.DueDate == null && w.StartDate != null && w.StartDate >= from && w.StartDate <= to))
            .OrderBy(w => w.DueDate ?? w.StartDate).ThenBy(w => w.Number)
            .Take(CalendarView.MaxItems)
            .Select(w => new { w.Key, w.Title, w.Type, w.StatusId, w.StartDate, w.DueDate, w.AssigneeId, w.RowVersion })
            .ToListAsync(ct);
        var unscheduled = await items.CountAsync(w => w.DueDate == null && w.StartDate == null, ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(caller.ProjectId, ct),
            rows.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        var statuses = info.Statuses.ToDictionary(s => s.Id);
        var shown = rows.ConvertAll(r =>
        {
            var status = statuses.GetValueOrDefault(r.StatusId);
            return new CalendarItem(r.Key, r.Title, r.Type, status?.Category ?? Domain.Common.StatusCategory.ToDo, status?.Name ?? "",
                (r.DueDate ?? r.StartDate)!.Value, r.StartDate, r.DueDate, r.AssigneeId is { } id ? people.GetValueOrDefault(id) : null, r.RowVersion);
        });
        return new CalendarView(info.Key, info.Name, info.Template, caller.CanManage, caller.CanContribute, caller.IsAdministrator, shown,
            unscheduled);
    }
}
