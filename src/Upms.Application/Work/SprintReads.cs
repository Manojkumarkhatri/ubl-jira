using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reads shared by the backlog, sprints and the Scrum board (Phase 3 research R10–R12), and the scope log they
/// write (R11).</summary>
internal sealed class SprintReads(IAppDbContext db, AssigneeReads assignees)
{
    /// <summary>A project's live cards: top-level stories, tasks and bugs (<c>IX_WorkItems_Backlog</c>).</summary>
    public IQueryable<WorkItem> Cards(long projectId) =>
        db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId && w.ParentId == null
            && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug));

    /// <summary>The backlog: open cards in no sprint or in a completed one (research R10).</summary>
    public IQueryable<WorkItem> BacklogCards(long projectId) =>
        Cards(projectId).Where(w => w.ResolvedAt == null
            && (w.SprintId == null || db.Sprints.Any(s => s.Id == w.SprintId && s.State == SprintState.Completed)));

    /// <summary>A planned or active sprint's cards, done ones included.</summary>
    public IQueryable<WorkItem> SprintCards(long projectId, long sprintId) => Cards(projectId).Where(w => w.SprintId == sprintId);

    public Task<Sprint?> ActiveSprintAsync(long projectId, CancellationToken ct) =>
        db.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.ProjectId == projectId && s.State == SprintState.Active, ct);

    /// <summary>Cards as backlog rows, in rank order.</summary>
    public async Task<List<BacklogRow>> RowsAsync(IQueryable<WorkItem> cards, long projectId, IReadOnlyList<StatusInfo> statuses,
        CancellationToken ct)
    {
        // The epic is a left join on its key: a correlated FirstOrDefault of several columns becomes a ROW_NUMBER over
        // every work item, which read the whole table at SC-002 volumes.
        var rows = await (
                from w in cards
                join c in db.WorkItems on w.ContainerId equals c.Id into epics
                from epic in epics.DefaultIfEmpty()
                orderby w.Rank, w.Id
                select new
                {
                    w.Key,
                    w.Type,
                    w.Title,
                    EpicKey = epic != null ? epic.Key : null,
                    EpicTitle = epic != null ? epic.Title : null,
                    EpicType = epic != null ? (WorkItemType?)epic.Type : null,
                    EpicColor = epic != null ? epic.Color : null,
                    w.AssigneeId,
                    w.Estimate,
                    w.StatusId,
                    SubtaskCount = db.WorkItems.Count(s => s.ParentId == w.Id),
                    w.RowVersion,
                })
            .ToListAsync(ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(projectId, ct),
            rows.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        return rows.ConvertAll(r => new BacklogRow(r.Key, r.Type, r.Title,
            r.EpicKey is { } epicKey ? new ContainerView(epicKey, r.EpicTitle!, r.EpicType!.Value, r.EpicColor) : null,
            r.AssigneeId is { } id ? people[id] : null, r.Estimate, WorkItemReads.ToOption(statuses, r.StatusId), r.SubtaskCount,
            r.RowVersion));
    }

    public async Task<SprintSection> SectionAsync(Sprint sprint, IReadOnlyList<StatusInfo> statuses, CancellationToken ct)
    {
        var items = await RowsAsync(SprintCards(sprint.ProjectId, sprint.Id), sprint.ProjectId, statuses, ct);
        return new SprintSection(sprint.Id, sprint.Number, sprint.Name, sprint.Goal, sprint.State, sprint.StartDate, sprint.EndDate,
            SprintTotals.Of(items), items, sprint.RowVersion);
    }

    /// <summary>The active sprint as the Scrum board shows it, with the planned sprints its open cards can go to.</summary>
    public async Task<ActiveSprintInfo?> ActiveInfoAsync(long projectId, CancellationToken ct)
    {
        if (await ActiveSprintAsync(projectId, ct) is not { } active)
        {
            return null;
        }

        var destinations = await db.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.State == SprintState.Planned)
            .OrderBy(s => s.Number)
            .Select(s => new SprintOption(s.Id, s.Name))
            .ToListAsync(ct);
        return new ActiveSprintInfo(active.Id, active.Number, active.Name, active.Goal, active.StartDate!.Value, active.EndDate!.Value,
            active.RowVersion, destinations);
    }

    /// <summary>Adds a scope event for a card of the active sprint, with its estimate now (research R11).</summary>
    public void Record(Sprint sprint, WorkItem card, SprintScopeKind kind, ChangeContext context) =>
        db.SprintScopeEvents.Add(SprintScopeEvent.Create(sprint.Id, card.Id, kind, card.Estimate, context));
}
