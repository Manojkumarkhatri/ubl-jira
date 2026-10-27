using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Work that goes with creating a project or switching its template (Phase 3 research R3), run in the Projects
/// module's transaction.</summary>
internal sealed class ProjectPlanSetup(
    IAppDbContext db,
    IProjectWorkflow workflow,
    IWorkItemNumberAllocator numbers,
    ICurrentUser currentUser,
    SearchIndexer indexer,
    TimeProvider time) : IProjectPlanSetup
{
    /// <summary>The stages a new Stage-gate project starts with, in order (Phase 3 FR-003).</summary>
    public static readonly IReadOnlyList<string> DefaultStages = ["Initiation", "Planning", "Execution", "Closure"];

    public async Task CreateDefaultStagesAsync(long projectId, string projectKey, CancellationToken ct)
    {
        var start = await workflow.FirstToDoStatusAsync(projectId, ct);
        var context = ChangeContext.New(currentUser.UserId ?? throw new InvalidOperationException("No signed-in user."), time.GetUtcNow());
        var ranks = Rank.Sequence(DefaultStages.Count);
        var stages = new List<WorkItem>(DefaultStages.Count);
        for (var i = 0; i < DefaultStages.Count; i++)
        {
            var number = await numbers.NextAsync(projectId, ct);
            stages.Add(WorkItem.Create(projectId, projectKey, number, WorkItemType.Stage, DefaultStages[i],
                new StatusRef(start.Id, start.Name, start.Category), ranks[i], ProjectTemplate.StageGate, context).Value!);
        }

        db.WorkItems.AddRange(stages);
        await db.SaveChangesAsync(ct);

        // Each stage ends in a gate, not requested and without approvers yet (FR-031).
        db.StageGates.AddRange(stages.Select(s => StageGate.Create(projectId, s.Id)));
        db.Watches.AddRange(stages.Select(s => Watch.Start(s.Id, context.ActorId, context.At))); // the creator watches (Phase 4 FR-011)
        foreach (var stage in stages)
        {
            await indexer.IndexAsync(stage, ct); // found by search at once (Phase 4 research R15)
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<WorkTypeSet, int>> TypeCountsAsync(long projectId, CancellationToken ct)
    {
        var counts = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .GroupBy(w => w.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return counts
            .Where(c => WorkTypeSets.FlagOf(c.Type) != WorkTypeSet.None)
            .ToDictionary(c => WorkTypeSets.FlagOf(c.Type), c => c.Count);
    }

    /// <summary>A planned or active sprint keeps a Scrum project from switching back to Kanban (Phase 3 FR-005).</summary>
    public Task<bool> HasOpenSprintsAsync(long projectId, CancellationToken ct) =>
        db.Sprints.AnyAsync(s => s.ProjectId == projectId && s.State != SprintState.Completed, ct);

    public async Task PrepareForScrumAsync(long projectId, CancellationToken ct)
    {
        var positions = (await workflow.StatusesAsync(projectId, ct)).ToDictionary(s => s.Id, s => s.Position);
        var cards = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ProjectId == projectId && w.ParentId == null
                && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug))
            .ToListAsync(ct);
        var ordered = cards
            .OrderBy(w => positions.GetValueOrDefault(w.StatusId, int.MaxValue))
            .ThenBy(w => w.Rank, StringComparer.Ordinal)
            .ThenBy(w => w.Id)
            .ToList();
        var ranks = Rank.Sequence(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Rerank(ranks[i]);
        }

        await db.SaveChangesAsync(ct);
    }
}
