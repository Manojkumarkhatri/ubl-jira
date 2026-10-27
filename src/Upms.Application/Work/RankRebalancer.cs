using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reassigns short, evenly spaced rank keys to a column, a Scrum project's cards or a sub-task list, keeping its
/// order (research R14; Phase 3 research R8).</summary>
internal sealed class RankRebalancer(IAppDbContext db, IProjectWorkflow workflow) : IRankRebalancer
{
    public async Task<int> RebalanceLongRanksAsync(CancellationToken ct)
    {
        const int threshold = Rank.RebalanceThreshold;
        var columns = await Cards()
            .Where(w => w.Rank.Length > threshold)
            .Select(w => new { w.ProjectId, w.StatusId })
            .Distinct()
            .ToListAsync(ct);
        var subtaskLists = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.ParentId != null && w.Rank.Length > threshold)
            .Select(w => w.ParentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var lists = 0;
        foreach (var project in columns.GroupBy(c => c.ProjectId))
        {
            var template = (await workflow.GetBoardInfoAsync(project.Key, ct)).Template;
            if (template == ProjectTemplate.Scrum)
            {
                await RebalanceProjectAsync(project.Key, ct);
                lists++;
                continue;
            }

            foreach (var column in project)
            {
                await RebalanceColumnAsync(column.ProjectId, column.StatusId, ct);
                lists++;
            }
        }

        foreach (var parentId in subtaskLists)
        {
            await RebalanceSubtasksAsync(parentId, ct);
        }

        return lists + subtaskLists.Count;
    }

    /// <summary>Renumbers the scope a card's rank belongs to: its column, or its whole Scrum project.</summary>
    public Task RebalanceAsync(long projectId, ProjectTemplate template, long statusId, CancellationToken ct) =>
        template == ProjectTemplate.Scrum ? RebalanceProjectAsync(projectId, ct) : RebalanceColumnAsync(projectId, statusId, ct);

    public async Task RebalanceColumnAsync(long projectId, long statusId, CancellationToken ct)
    {
        var items = await Cards(tracked: true)
            .Where(w => w.ProjectId == projectId && w.StatusId == statusId)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        await AssignAsync(items, ct);
    }

    /// <summary>Renumbers every card of a Scrum project, deleted ones included, in their project-wide order.</summary>
    public async Task RebalanceProjectAsync(long projectId, CancellationToken ct)
    {
        var items = await Cards(tracked: true)
            .Where(w => w.ProjectId == projectId)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        await AssignAsync(items, ct);
    }

    public async Task RebalanceSubtasksAsync(long parentId, CancellationToken ct)
    {
        var items = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ParentId == parentId)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        await AssignAsync(items, ct);
    }

    // Cards (top-level stories, tasks and bugs), deleted ones included; epics, stages and milestones are not in any
    // card order (Phase 3 research R4).
    private IQueryable<WorkItem> Cards(bool tracked = false)
    {
        var items = db.WorkItems.IgnoreQueryFilters();
        return (tracked ? items : items.AsNoTracking()).Where(w => w.ParentId == null
            && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug));
    }

    private async Task AssignAsync(List<WorkItem> items, CancellationToken ct)
    {
        var ranks = Rank.Sequence(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Rerank(ranks[i]);
        }

        await db.SaveChangesAsync(ct);
    }
}
