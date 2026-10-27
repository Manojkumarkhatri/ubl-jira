using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reads the caller's assigned items through <c>IX_WorkItems_Assignee_Live</c>, keeps the projects they can see
/// and orders them in memory, since project names live in the Projects module (Phase 2 research R13). A person has
/// at most a few hundred open assigned items.</summary>
internal sealed class MyTasksService(IAppDbContext db, ICallerContext caller, IProjectTeam team, IProjectWorkflow workflow,
    IUserDirectory users)
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
        var items = await RowsAsync(pageIds, projects.ToDictionary(p => p.Key, p => p.Value.Project), ct);
        return new Page<MyTaskRow>(items, ordered.Count, page.Page, page.PageSize);
    }

    public async Task<Result<MyWorkSummary>> SummaryAsync(DateOnly today, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        // Open items, and those finished from the start of the day 6 days ago (the last 7 days, counting today).
        var since = new DateTimeOffset(today.AddDays(-6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var assigned = await db.WorkItems.AsNoTracking()
            .Where(w => w.AssigneeId == me.UserId && (w.ResolvedAt == null || w.ResolvedAt >= since))
            .Select(w => new { w.Id, w.ProjectId, w.StatusId, w.DueDate, w.Priority, w.Number, IsOpen = w.ResolvedAt == null })
            .ToListAsync(ct);
        if (assigned.Count == 0)
        {
            return MyWorkSummary.Empty;
        }

        var projects = (await team.VisibleAmongAsync(me.UserId, assigned.Select(a => a.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var mine = assigned.Where(a => projects.ContainsKey(a.ProjectId)).ToList();
        var open = mine.Where(a => a.IsOpen).ToList();
        var statuses = await workflow.StatusesAsync(open.Select(a => a.ProjectId).Distinct().ToList(), ct);
        var categories = open.ConvertAll(a => WorkItemReads.ToOption(statuses.GetValueOrDefault(a.ProjectId) ?? [], a.StatusId).Category);
        var soon = today.AddDays(7);

        var upcoming = open.Where(a => a.DueDate <= soon)
            .OrderBy(a => a.DueDate).ThenBy(a => a.Priority).ThenBy(a => a.Number)
            .Take(MyWorkSummary.Listed).Select(a => a.Id).ToList();
        var first = open.OrderBy(a => a.DueDate is null).ThenBy(a => a.DueDate).ThenBy(a => a.Priority).ThenBy(a => a.Number)
            .Take(MyWorkSummary.Listed).Select(a => a.Id).ToList();
        var listed = upcoming.Union(first).ToList();
        var rows = listed.Zip(await RowsAsync(listed, projects, ct)).ToDictionary(r => r.First, r => r.Second);

        var bySpace = open.GroupBy(a => a.ProjectId)
            .Select(g => new SpaceWorkCount(projects[g.Key].Key, projects[g.Key].Name, g.Count()))
            .OrderByDescending(s => s.Open).ThenBy(s => s.ProjectName, StringComparer.CurrentCultureIgnoreCase)
            .Take(6).ToList();
        return new MyWorkSummary(
            open.Count,
            open.Count(a => a.DueDate < today),
            open.Count(a => a.DueDate >= today && a.DueDate <= soon),
            mine.Count(a => !a.IsOpen),
            categories.Count(c => c == StatusCategory.ToDo),
            categories.Count(c => c == StatusCategory.InProgress),
            upcoming.ConvertAll(id => rows[id]),
            first.ConvertAll(id => rows[id]),
            bySpace);
    }

    /// <summary>The rows of these items, in this order, with their space, parent and status.</summary>
    private async Task<List<MyTaskRow>> RowsAsync(List<long> ids, Dictionary<long, ProjectRef> projects, CancellationToken ct)
    {
        var rows = await db.WorkItems.AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                w.Key,
                w.Title,
                w.StatusId,
                w.Priority,
                w.DueDate,
                w.Type,
                ParentKey = db.WorkItems.Where(p => p.Id == w.ParentId).Select(p => p.Key).FirstOrDefault(),
            })
            .ToDictionaryAsync(r => r.Id, ct);
        var statuses = await workflow.StatusesAsync(rows.Values.Select(r => r.ProjectId).Distinct().ToList(), ct);
        return ids.ConvertAll(id =>
        {
            var row = rows[id];
            var project = projects[row.ProjectId];
            return new MyTaskRow(project.Key, project.Name, row.Key, row.Title, row.ParentKey,
                WorkItemReads.ToOption(statuses.GetValueOrDefault(row.ProjectId) ?? [], row.StatusId), row.Priority, row.DueDate, row.Type);
        });
    }

    public async Task<Result<IReadOnlyList<WaitingGateRow>>> ListGatesAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        // Pending requests (IX_GateRequests_Pending) of gates the caller approves, which they have not decided on and
        // did not request themselves when the gate excludes the requester (Phase 3 FR-041, research R17). Deleted stages'
        // requests are cancelled, and the stage filter leaves out any other.
        var userId = me.UserId;
        var waiting = await (
                from r in db.GateRequests.AsNoTracking()
                join g in db.StageGates on r.GateId equals g.Id
                join s in db.WorkItems on g.StageId equals s.Id
                where r.Outcome == GateOutcome.Pending
                    && db.GateApprovers.Any(a => a.GateId == g.Id && a.UserId == userId)
                    && !db.GateDecisions.Any(d => d.RequestId == r.Id && d.ApproverId == userId)
                    && !(g.ExcludeRequester && r.RequestedById == userId)
                orderby r.RequestedAt, r.Id
                select new { g.ProjectId, s.Key, s.Title, r.RequestedById, r.RequestedAt })
            .ToListAsync(ct);
        if (waiting.Count == 0)
        {
            return Result<IReadOnlyList<WaitingGateRow>>.Ok([]);
        }

        var projects = (await team.VisibleAmongAsync(userId, waiting.Select(w => w.ProjectId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var names = await users.GetAsync(waiting.Select(w => w.RequestedById).Distinct().ToList(), ct);
        return waiting
            .Where(w => projects.ContainsKey(w.ProjectId))
            .Select(w => new WaitingGateRow(projects[w.ProjectId].Key, projects[w.ProjectId].Name, w.Key, w.Title,
                WorkItemReads.NameOf(names, w.RequestedById), w.RequestedAt))
            .ToList();
    }
}
