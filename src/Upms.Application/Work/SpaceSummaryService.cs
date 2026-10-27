using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The Summary page (Jira parity pass FR-022; research R8): each section is one grouped query over the space's live
/// items, which <c>IX_WorkItems_Summary</c> covers; recent activity reads <c>IX_WorkItemChanges_OccurredAt</c> backwards.</summary>
internal sealed class SpaceSummaryService(IAppDbContext db, IProjectAccess access, IProjectWorkflow workflow, IUserDirectory users,
    TimeProvider time) : ISpaceSummaryService
{
    public const int ActivityShown = 20;
    private static readonly TimeSpan ActivityWindow = TimeSpan.FromDays(30);

    public async Task<Result<SpaceSummary>> GetAsync(string spaceKey, DateOnly today, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var doneIds = info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var items = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == caller.ProjectId); // live items only
        var now = time.GetUtcNow();
        // Whole UTC days, today and the 6 before, as the search page's date ranges count them, so each count matches its link.
        var since = new DateTimeOffset(DateOnly.FromDateTime(now.UtcDateTime).AddDays(-6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dueBy = today.AddDays(7);

        var counts = await items
            .GroupBy(_ => 1)
            .Select(g => new SummaryCounts(
                g.Count(w => w.ResolvedAt != null && w.ResolvedAt >= since),
                g.Count(w => w.UpdatedAt >= since),
                g.Count(w => w.CreatedAt >= since),
                g.Count(w => w.ResolvedAt == null && w.DueDate != null && w.DueDate <= dueBy)))
            .SingleOrDefaultAsync(ct) ?? new SummaryCounts(0, 0, 0, 0);

        var byStatus = await items.GroupBy(w => w.StatusId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var categoryOf = info.Statuses.ToDictionary(s => s.Id, s => s.Category);
        var overview = Enum.GetValues<StatusCategory>()
            .Select(c => new CategoryCount(c, byStatus.Where(s => categoryOf.GetValueOrDefault(s.Key) == c).Sum(s => s.Count)))
            .ToList();

        var byPriority = await items.GroupBy(w => w.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var priorities = Enum.GetValues<Priority>()
            .Select(p => new PriorityCount(p, byPriority.FirstOrDefault(x => x.Key == p)?.Count ?? 0))
            .ToList();

        var byType = await items.GroupBy(w => w.Type).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var types = CreateWorkService.FormOrder
            .Select(t => new TypeCount(t, byType.FirstOrDefault(x => x.Key == t)?.Count ?? 0))
            .Where(t => t.Count > 0)
            .ToList();

        var statuses = info.Statuses
            .Select(s => new StatusCount(s.Name, s.Category, byStatus.FirstOrDefault(x => x.Key == s.Id)?.Count ?? 0))
            .ToList();
        return new SpaceSummary(info.Key, info.Name, info.Template, counts, overview, await ActivityAsync(caller.ProjectId, info, now, ct),
            priorities, types, await WorkloadAsync(items, ct), await ContainersAsync(items, doneIds, ct), caller.CanManage,
            caller.IsAdministrator)
        { Statuses = statuses };
    }

    /// <summary>Open items per person, most first, then the unassigned ones.</summary>
    private async Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(IQueryable<WorkItem> items, CancellationToken ct)
    {
        var open = await items.Where(w => w.ResolvedAt == null)
            .GroupBy(w => w.AssigneeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var people = await users.GetAsync(open.Where(o => o.Key != null).Select(o => o.Key!.Value).ToList(), ct);
        return open
            .OrderBy(o => o.Key is null)
            .ThenByDescending(o => o.Count)
            .ThenBy(o => o.Key is { } id && people.TryGetValue(id, out var p) ? p.DisplayName : "", StringComparer.CurrentCultureIgnoreCase)
            .Select(o => new WorkloadRow(
                o.Key is { } id && people.TryGetValue(id, out var person) ? AssigneeRef.Of(id, person.DisplayName, person.IsActive) : null,
                o.Count))
            .ToList();
    }

    /// <summary>Each epic's or stage's progress, with Phase 3's rule: standard items other than milestones, done of all.</summary>
    private static async Task<IReadOnlyList<ContainerProgress>> ContainersAsync(IQueryable<WorkItem> items, List<long> doneIds,
        CancellationToken ct)
    {
        var containers = await items
            .Where(w => w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage)
            .Select(w => new { w.Id, w.Key, w.Title, w.Type, w.StatusId, w.Rank, w.Number })
            .ToListAsync(ct);
        if (containers.Count == 0)
        {
            return [];
        }

        var progress = await items
            .Where(w => w.ContainerId != null && w.ParentId == null && w.Type != WorkItemType.Milestone
                && w.Type != WorkItemType.Epic && w.Type != WorkItemType.Stage)
            .GroupBy(w => w.ContainerId!.Value)
            .Select(g => new { g.Key, Total = g.Count(), Done = g.Count(w => doneIds.Contains(w.StatusId)) })
            .ToDictionaryAsync(g => g.Key, ct);
        return containers
            .OrderBy(c => c.Type == WorkItemType.Stage ? c.Rank : "", StringComparer.Ordinal)
            .ThenBy(c => c.Number)
            .Select(c => new ContainerProgress(c.Key, c.Title, c.Type,
                new Progress(progress.GetValueOrDefault(c.Id)?.Done ?? 0, progress.GetValueOrDefault(c.Id)?.Total ?? 0,
                    doneIds.Contains(c.StatusId))))
            .ToList();
    }

    private async Task<IReadOnlyList<ActivityEntry>> ActivityAsync(long projectId, BoardInfo info, DateTimeOffset now, CancellationToken ct)
    {
        var since = now - ActivityWindow;
        var changes = await (
                from change in db.WorkItemChanges.AsNoTracking()
                where change.OccurredAt >= since
                join item in db.WorkItems.AsNoTracking() on change.WorkItemId equals item.Id
                where item.ProjectId == projectId
                orderby change.OccurredAt descending, change.Id descending
                select new
                {
                    item.Key,
                    item.Title,
                    item.Type,
                    item.StatusId,
                    change.ActorId,
                    change.OccurredAt,
                    change.Field,
                    change.OldValue,
                    change.NewValue,
                    change.Note,
                })
            .Take(ActivityShown)
            .ToListAsync(ct);
        var actors = await users.GetAsync(changes.Select(c => c.ActorId).Distinct().ToList(), ct);
        var statuses = info.Statuses.ToDictionary(s => s.Id);
        return changes.ConvertAll(c => new ActivityEntry(c.Key, c.Title, c.Type,
            actors.TryGetValue(c.ActorId, out var actor) ? actor.DisplayName : "Unknown user", c.OccurredAt, c.Field, c.OldValue,
            c.NewValue, c.Note)
        {
            ActorId = c.ActorId,
            ItemStatus = statuses.GetValueOrDefault(c.StatusId)?.Name ?? "",
            ItemCategory = statuses.GetValueOrDefault(c.StatusId)?.Category ?? StatusCategory.ToDo,
        });
    }
}
