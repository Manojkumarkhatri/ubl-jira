using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Work;

/// <summary>Reads the caller's assigned items through <c>IX_WorkItems_Assignee_Live</c>, keeps the projects they can see
/// and orders them in memory, since project names live in the Projects module (Phase 2 research R13). A person has
/// at most a few hundred open assigned items.</summary>
internal sealed class MyTasksService(IAppDbContext db, ICallerContext caller, IProjectTeam team, IProjectWorkflow workflow)
    : IMyTasksService
{
    public async Task<Result<Page<MyTaskRow>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        page = page.Normalized();
        // Open = not resolved: an item has a ResolvedAt exactly while it is in a "done" column (Phase 1 FR-027).
        var assigned = await db.WorkItems.AsNoTracking()
            .Where(w => w.AssigneeId == me.UserId && w.ResolvedAt == null)
            .Select(w => new { w.Id, w.ProjectId, w.DueDate, w.Priority, w.Number })
            .ToListAsync(ct);
        if (assigned.Count == 0)
        {
            return Page<MyTaskRow>.Empty(page);
        }

        var visible = await team.VisibleAmongAsync(me.UserId, assigned.Select(a => a.ProjectId).Distinct().ToList(), ct);
        var projects = visible
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select((project, order) => (Project: project, Order: order))
            .ToDictionary(p => p.Project.Id);
        var ordered = assigned
            .Where(a => projects.ContainsKey(a.ProjectId))
            .OrderBy(a => projects[a.ProjectId].Order)
            .ThenBy(a => a.DueDate is null)
            .ThenBy(a => a.DueDate)
            .ThenBy(a => a.Priority)
            .ThenBy(a => a.Number)
            .ToList();
        var pageIds = ordered.Skip(page.Skip).Take(page.PageSize).Select(a => a.Id).ToList();
        var rows = await db.WorkItems.AsNoTracking()
            .Where(w => pageIds.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                w.Key,
                w.Title,
                w.StatusId,
                w.Priority,
                w.DueDate,
                ParentKey = db.WorkItems.Where(p => p.Id == w.ParentId).Select(p => p.Key).FirstOrDefault(),
            })
            .ToDictionaryAsync(r => r.Id, ct);
        var statuses = await workflow.StatusesAsync(rows.Values.Select(r => r.ProjectId).Distinct().ToList(), ct);
        var items = pageIds.ConvertAll(id =>
        {
            var row = rows[id];
            var project = projects[row.ProjectId].Project;
            return new MyTaskRow(project.Key, project.Name, row.Key, row.Title, row.ParentKey,
                WorkItemReads.ToOption(statuses.GetValueOrDefault(row.ProjectId) ?? [], row.StatusId), row.Priority, row.DueDate);
        });
        return new Page<MyTaskRow>(items, ordered.Count, page.Page, page.PageSize);
    }
}
