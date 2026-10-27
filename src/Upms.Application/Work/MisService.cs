using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The MIS (<see cref="IMisService"/>): a handful of grouped queries over the live items of the spaces in scope.
/// Items are the work other than epics, stages and milestones; "open" means not resolved, as everywhere else.</summary>
internal sealed class MisService(
    IAppDbContext db,
    ICallerContext caller,
    IPortfolioReads portfolios,
    IProjectWorkflow workflow,
    IUserDirectory users,
    TimeProvider time) : IMisService
{
    public async Task<Result<MisReport>> BuildAsync(MisLevel level, string? spaceKey, long? id, DateOnly today, CancellationToken ct)
    {
        var scope = await portfolios.ScopeAsync(level, spaceKey, id, ct);
        if (!scope.IsSuccess)
        {
            return scope.Error!;
        }

        var spaces = scope.Value!.Spaces;
        var ids = spaces.Select(s => s.ProjectId).ToList();
        var keyOf = spaces.ToDictionary(s => s.ProjectId, s => s.Key);
        var statusById = (await workflow.StatusesAsync(ids, ct)).Values.SelectMany(s => s).ToDictionary(s => s.Id);
        // Never empty (no spaces in scope): SQL Server refuses the count an empty list would make.
        var doneIds = statusById.Values.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).Append(-1).ToList();
        var items = db.WorkItems.AsNoTracking().Where(w => ids.Contains(w.ProjectId)); // live items only
        var work = items.Where(w => w.Type != WorkItemType.Epic && w.Type != WorkItemType.Stage && w.Type != WorkItemType.Milestone);
        var dueSoonBy = today.AddDays(MisRules.DueSoonDays);
        var now = time.GetUtcNow();
        var since = now.AddDays(-30);

        var byStatus = await work.GroupBy(w => new { w.ProjectId, w.StatusId })
            .Select(g => new { g.Key.ProjectId, g.Key.StatusId, Count = g.Count() })
            .ToListAsync(ct);
        var dates = await work.GroupBy(w => w.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Open = g.Count(w => w.ResolvedAt == null),
                Overdue = g.Count(w => w.ResolvedAt == null && w.DueDate != null && w.DueDate < today),
                DueSoon = g.Count(w => w.ResolvedAt == null && w.DueDate != null && w.DueDate >= today && w.DueDate <= dueSoonBy),
                LastUpdated = g.Max(w => (DateTimeOffset?)w.UpdatedAt),
            })
            .ToDictionaryAsync(d => d.ProjectId, ct);
        var openMilestones = await items
            .Where(w => w.Type == WorkItemType.Milestone && w.ResolvedAt == null && w.DueDate != null)
            .OrderBy(w => w.DueDate).ThenBy(w => w.Number)
            .Take(MisRules.MaxRows)
            .Select(w => new { w.ProjectId, w.Key, w.Title, w.StatusId, w.AssigneeId, w.DueDate, w.Priority })
            .ToListAsync(ct);
        var containers = await items
            .Where(w => w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage)
            .OrderBy(w => w.ProjectId).ThenBy(w => w.Type == WorkItemType.Stage ? w.Rank : "").ThenBy(w => w.Number)
            .Take(MisRules.MaxRows)
            .Select(w => new { w.Id, w.ProjectId, w.Key, w.Title, w.Type, w.StatusId, w.AssigneeId, w.StartDate, w.DueDate, w.ResolvedAt })
            .ToListAsync(ct);
        var progress = await items
            .Where(w => w.ContainerId != null && w.ParentId == null && w.Type != WorkItemType.Milestone
                && w.Type != WorkItemType.Epic && w.Type != WorkItemType.Stage)
            .GroupBy(w => w.ContainerId!.Value)
            .Select(g => new { g.Key, Total = g.Count(), Done = g.Count(w => doneIds.Contains(w.StatusId)) })
            .ToDictionaryAsync(g => g.Key, ct);
        var overdue = await work
            .Where(w => w.ResolvedAt == null && w.DueDate != null && w.DueDate < today)
            .OrderBy(w => w.DueDate).ThenBy(w => w.ProjectId).ThenBy(w => w.Number)
            .Take(MisRules.MaxRows)
            .Select(w => new { w.ProjectId, w.Key, w.Type, w.Title, w.StatusId, w.AssigneeId, w.DueDate, w.Priority })
            .ToListAsync(ct);
        var workload = await work.Where(w => w.AssigneeId != null)
            .GroupBy(w => w.AssigneeId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Open = g.Count(w => w.ResolvedAt == null),
                Overdue = g.Count(w => w.ResolvedAt == null && w.DueDate != null && w.DueDate < today),
                Done = g.Count(w => w.ResolvedAt != null && w.ResolvedAt >= since),
            })
            .Where(p => p.Open > 0 || p.Done > 0)
            .ToListAsync(ct);

        var me = await caller.GetAsync(ct);
        var people = await users.GetAsync(workload.Select(p => p.UserId)
            .Concat(overdue.Where(o => o.AssigneeId != null).Select(o => o.AssigneeId!.Value))
            .Concat(containers.Where(c => c.AssigneeId != null).Select(c => c.AssigneeId!.Value))
            .Concat(openMilestones.Where(m => m.AssigneeId != null).Select(m => m.AssigneeId!.Value))
            .Concat(me is null ? [] : [me.UserId])
            .ToList(), ct);
        string? NameOf(Guid? userId) => userId is { } u && people.TryGetValue(u, out var p) ? p.DisplayName : null;
        string StatusName(long statusId) => statusById.TryGetValue(statusId, out var s) ? s.Name : "";
        StatusCategory CategoryOf(long statusId) => statusById.TryGetValue(statusId, out var s) ? s.Category : StatusCategory.ToDo;

        var spaceRows = spaces.Select(space =>
        {
            int Count(StatusCategory category) =>
                byStatus.Where(b => b.ProjectId == space.ProjectId && CategoryOf(b.StatusId) == category).Sum(b => b.Count);
            var toDo = Count(StatusCategory.ToDo);
            var inProgress = Count(StatusCategory.InProgress);
            var done = Count(StatusCategory.Done);
            var dated = dates.GetValueOrDefault(space.ProjectId);
            var milestones = openMilestones.Where(m => m.ProjectId == space.ProjectId).ToList();
            var next = milestones.FirstOrDefault();
            var health = MisRules.Health(toDo + inProgress + done, dated?.Open ?? 0, dated?.Overdue ?? 0,
                milestones.Exists(m => m.DueDate < today), milestones.Exists(m => m.DueDate >= today && m.DueDate <= today.AddDays(7)));
            return new MisSpaceRow(space.Department, space.Portfolio, space.Key, space.Name, space.Template, string.Join(", ", space.Leads),
                toDo + inProgress + done, toDo, inProgress, done, dated?.Overdue ?? 0, dated?.DueSoon ?? 0, next?.Title, next?.DueDate, health,
                dated?.LastUpdated);
        }).ToList();

        var totals = new MisTotals(
            spaceRows.Count,
            spaceRows.Sum(s => s.Items),
            spaceRows.Sum(s => s.ToDo),
            spaceRows.Sum(s => s.InProgress),
            spaceRows.Sum(s => s.Done),
            spaceRows.Sum(s => s.Overdue),
            spaceRows.Sum(s => s.DueSoon),
            openMilestones.Count(m => m.DueDate <= today.AddDays(MisRules.MilestoneWindowDays)),
            spaceRows.Count(s => s.Health == MisHealth.OnTrack),
            spaceRows.Count(s => s.Health == MisHealth.AtRisk),
            spaceRows.Count(s => s.Health == MisHealth.OffTrack));

        var containerRows = containers.ConvertAll(c =>
        {
            var p = progress.GetValueOrDefault(c.Id);
            return new MisContainerRow(keyOf[c.ProjectId], c.Key, c.Title, TypeName(c.Type), StatusName(c.StatusId), CategoryOf(c.StatusId),
                NameOf(c.AssigneeId), c.StartDate, c.DueDate, p?.Total ?? 0, p?.Done ?? 0,
                c.ResolvedAt is null && c.DueDate is { } due && due < today);
        });
        var overdueRows = overdue.ConvertAll(o => new MisItemRow(keyOf[o.ProjectId], o.Key, TypeName(o.Type), o.Title, StatusName(o.StatusId),
            CategoryOf(o.StatusId), NameOf(o.AssigneeId), o.DueDate, today.DayNumber - o.DueDate!.Value.DayNumber, o.Priority.ToString()));
        var milestoneRows = openMilestones
            .Where(m => m.DueDate <= today.AddDays(MisRules.MilestoneWindowDays))
            .Select(m => new MisItemRow(keyOf[m.ProjectId], m.Key, "Milestone", m.Title, StatusName(m.StatusId), CategoryOf(m.StatusId),
                NameOf(m.AssigneeId), m.DueDate, m.DueDate!.Value.DayNumber - today.DayNumber, m.Priority.ToString()))
            .ToList();
        var personRows = workload
            .Select(p => new MisPersonRow(NameOf(p.UserId) ?? "Unknown person", p.Open, p.Overdue, p.Done))
            .OrderByDescending(p => p.Overdue).ThenByDescending(p => p.Open).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new MisReport(level, scope.Value.Name, today, now, NameOf(me?.UserId) ?? "", totals, spaceRows, containerRows, overdueRows,
            milestoneRows, personRows);
    }

    private static string TypeName(WorkItemType type) => type == WorkItemType.Subtask ? "Sub-task" : type.ToString();
}
