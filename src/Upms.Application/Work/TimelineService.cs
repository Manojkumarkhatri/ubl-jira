using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reads a project's scheduled items from <c>IX_WorkItems_Project_Live</c> and groups sub-tasks under their
/// item; rescheduling goes through <see cref="WorkItem.Schedule"/>, like the drawer's dates (Phase 2 research R8, R12).
/// Since Phase 3 the epics or stages come first, each with its items, progress and gate, then the items in no container;
/// a Scrum project adds its sprints, and dependencies between shown items are drawn with their late flags (research
/// R19, R20).</summary>
internal sealed class TimelineService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    AssigneeReads assignees,
    TimeProvider time) : ITimelineService
{
    public const int UnscheduledPageSize = 50;

    public async Task<Result<TimelineView>> GetAsync(string projectKey, bool hideCompleted, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var doneIds = DoneIds(info);
        var live = Live(info.ProjectId, hideCompleted, doneIds);

        // Rows: scheduled items (not containers) and the undated items heading scheduled sub-tasks, by container.
        var scheduled = await Read(live.Where(w => (w.StartDate != null || w.DueDate != null)
            && w.Type != WorkItemType.Epic && w.Type != WorkItemType.Stage), ct);
        var scheduledSubtasks = scheduled.Where(i => i.ParentId != null).ToLookup(i => i.ParentId!.Value);
        var headIds = scheduledSubtasks.Select(g => g.Key).Except(scheduled.Where(i => i.ParentId == null).Select(i => i.Id)).ToList();
        var heads = scheduled.Where(i => i.ParentId == null).Concat(await Read(live.Where(w => headIds.Contains(w.Id)), ct)).ToList();
        var rows = heads
            .Select(head => (Head: head, Subtasks: scheduledSubtasks[head.Id].OrderBy(FirstDate).ThenBy(i => i.DueDate).ThenBy(i => i.Number).ToList()))
            .OrderBy(row => FirstDate(row.Head) ?? row.Subtasks.Min(FirstDate))
            .ThenBy(row => row.Head.DueDate ?? DateOnly.MaxValue)
            .ThenBy(row => row.Head.Number)
            .ToList();
        // Groups: the epics or stages, with their rows and their undated items. Open work of a hidden (completed)
        // container shows with the items in no container.
        var containers = await Read(live.Where(w => w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage), ct);
        var containerIds = containers.Select(c => c.Id).ToList();
        var loose = rows.Where(r => r.Head.ContainerId is not { } id || !containerIds.Contains(id)).ToList();
        var shownLoose = loose.Skip(Math.Max(0, loose.Count - TimelineView.MaxRows)).ToList();
        var inContainers = rows.Where(r => r.Head.ContainerId is { } id && containerIds.Contains(id)).ToLookup(r => r.Head.ContainerId!.Value);
        var shownHeadIds = shownLoose.Concat(inContainers.SelectMany(g => g)).Select(r => r.Head.Id).ToList();
        var all = db.WorkItems.AsNoTracking();
        var undatedInContainers = (await Read(live.Where(w => w.ParentId == null && w.ContainerId != null && containerIds.Contains(w.ContainerId.Value)
                && w.StartDate == null && w.DueDate == null && !all.Any(c => c.ParentId == w.Id && (c.StartDate != null || c.DueDate != null))), ct))
            .ToLookup(i => i.ContainerId!.Value);
        var unscheduledSubtasks = (await Read(live.Where(w => w.ParentId != null && shownHeadIds.Contains(w.ParentId.Value)
                && w.StartDate == null && w.DueDate == null), ct))
            .ToLookup(i => i.ParentId!.Value);
        var unscheduled = await UnscheduledAsync(info.ProjectId, hideCompleted, doneIds, PageRequest.First, ct);
        var progress = await all
            .Where(w => w.ContainerId != null && containerIds.Contains(w.ContainerId.Value) && w.Type != WorkItemType.Milestone)
            .GroupBy(w => w.ContainerId!.Value)
            .Select(g => new { ContainerId = g.Key, Total = g.Count(), Done = g.Count(w => w.ResolvedAt != null) })
            .ToDictionaryAsync(g => g.ContainerId, ct);
        var gates = await db.StageGates.AsNoTracking()
            .Where(g => containerIds.Contains(g.StageId))
            .ToDictionaryAsync(g => g.StageId, g => g.State, ct);

        // Dependencies of every shown item: arrows between shown bars, and what each item waits for.
        var shownItems = shownLoose.Concat(inContainers.SelectMany(g => g)).SelectMany(r => r.Subtasks.Prepend(r.Head)).Concat(containers).ToList();
        var shownIds = shownItems.Select(i => i.Id).ToHashSet();
        var links = await db.Dependencies.AsNoTracking()
            .Where(d => d.ProjectId == info.ProjectId)
            .Select(d => new { d.PredecessorId, d.SuccessorId })
            .ToListAsync(ct);
        var linkedIds = links.SelectMany(l => new[] { l.PredecessorId, l.SuccessorId }).Distinct().ToList();
        var linked = (await Read(all.Where(w => linkedIds.Contains(w.Id)), ct)).ToDictionary(i => i.Id);
        bool IsLate(long predecessorId, long successorId) =>
            linked.TryGetValue(predecessorId, out var waitedFor) && linked.TryGetValue(successorId, out var waiting)
            && DependencyGraph.IsLate(!doneIds.Contains(waitedFor.StatusId), waitedFor.DueDate, waiting.StartDate, waiting.DueDate);
        var waitsFor = links.Where(l => linked.ContainsKey(l.PredecessorId) && linked.ContainsKey(l.SuccessorId))
            .ToLookup(l => l.SuccessorId, l => new WaitingOn(linked[l.PredecessorId].Key, IsLate(l.PredecessorId, l.SuccessorId)));

        var team = await assignees.TeamAsync(info.ProjectId, ct);
        var people = await assignees.DescribeAsync(team,
            shownItems
                .Concat(unscheduledSubtasks.SelectMany(g => g))
                .Concat(undatedInContainers.SelectMany(g => g))
                .Concat(unscheduled.Items)
                .Where(i => i.AssigneeId != null)
                .Select(i => i.AssigneeId!.Value),
            ct);
        TimelineItem ToItem(ItemData data) => data.ToItem(info.Statuses, doneIds, people) with { WaitsFor = waitsFor[data.Id].ToList() };
        TimelineRow ToRow((ItemData Head, List<ItemData> Subtasks) row) => new(ToItem(row.Head), row.Subtasks.ConvertAll(ToItem),
            unscheduledSubtasks[row.Head.Id].OrderBy(i => i.Number).Select(ToItem).ToList());

        var groups = OrderContainers(containers).Select(c =>
        {
            var counts = progress.GetValueOrDefault(c.Id);
            var gate = c.Type == WorkItemType.Stage && c.DueDate is { } due && gates.TryGetValue(c.Id, out var state) ? new GateMarker(due, state) : null;
            return new TimelineGroup(ToItem(c), new Progress(counts?.Done ?? 0, counts?.Total ?? 0, doneIds.Contains(c.StatusId)), gate,
                inContainers[c.Id].Select(ToRow).ToList(), undatedInContainers[c.Id].OrderBy(i => i.Number).Select(ToItem).ToList());
        }).ToList();

        var dependencies = links
            .Where(l => shownIds.Contains(l.PredecessorId) && shownIds.Contains(l.SuccessorId)
                && linked[l.PredecessorId].IsScheduled && linked[l.SuccessorId].IsScheduled)
            .Take(TimelineView.MaxDependencies)
            .Select(l => new TimelineDependency(linked[l.PredecessorId].Key, linked[l.SuccessorId].Key, IsLate(l.PredecessorId, l.SuccessorId)))
            .ToList();
        var sprints = info.Template == ProjectTemplate.Scrum
            ? await db.Sprints.AsNoTracking()
                .Where(s => s.ProjectId == info.ProjectId && s.State != SprintState.Planned && s.StartDate != null && s.EndDate != null)
                .OrderBy(s => s.Number)
                .Select(s => new SprintBar(s.Name, s.StartDate!.Value, s.EndDate!.Value, s.State))
                .ToListAsync(ct)
            : [];

        return new TimelineView(info.Key, info.Name, caller.CanContribute, caller.CanManage, caller.IsAdministrator,
            shownLoose.ConvertAll(ToRow), loose.Count,
            new Page<TimelineItem>(unscheduled.Items.Select(ToItem).ToList(), unscheduled.TotalCount, unscheduled.PageNumber, unscheduled.PageSize),
            hideCompleted, info.Template)
        {
            Groups = groups,
            Sprints = sprints,
            Dependencies = dependencies,
            Statuses = info.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(),
        };
    }

    public async Task<Result<Page<TimelineItem>>> ListUnscheduledAsync(string projectKey, bool hideCompleted, PageRequest page,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var info = await workflow.GetBoardInfoAsync(allowed.Value!.ProjectId, ct);
        var doneIds = DoneIds(info);
        var unscheduled = await UnscheduledAsync(info.ProjectId, hideCompleted, doneIds, page, ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(info.ProjectId, ct),
            unscheduled.Items.Where(i => i.AssigneeId != null).Select(i => i.AssigneeId!.Value), ct);
        return new Page<TimelineItem>(unscheduled.Items.Select(i => i.ToItem(info.Statuses, doneIds, people)).ToList(),
            unscheduled.TotalCount, unscheduled.PageNumber, unscheduled.PageSize);
    }

    public async Task<Result<TimelineItem>> RescheduleAsync(string workItemKey, DateOnly? start, DateOnly? due, byte[] expectedVersion,
        CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(item.Id, workItemKey, ct);
        }

        // A milestone keeps one date: a start date is refused (Phase 3 FR-043).
        if (item.Schedule(start, due, ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow())) is { } invalid)
        {
            return invalid.ToAppError();
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(item.Id, workItemKey, ct);
        }

        return await CurrentAsync(item.Id, ct);
    }

    private IQueryable<WorkItem> Live(long projectId, bool hideCompleted, List<long> doneIds)
    {
        var items = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId);
        return hideCompleted ? items.Where(w => !doneIds.Contains(w.StatusId)) : items;
    }

    /// <summary>Items in no epic or stage, not themselves epics or stages, with no dates and no scheduled sub-tasks.</summary>
    private async Task<Page<ItemData>> UnscheduledAsync(long projectId, bool hideCompleted, List<long> doneIds, PageRequest page,
        CancellationToken ct)
    {
        page = new PageRequest(page.Page, UnscheduledPageSize).Normalized();
        var all = db.WorkItems.AsNoTracking();
        var query = Live(projectId, hideCompleted, doneIds)
            .Where(w => w.ParentId == null && w.ContainerId == null && w.Type != WorkItemType.Epic && w.Type != WorkItemType.Stage
                && w.StartDate == null && w.DueDate == null
                && !all.Any(c => c.ParentId == w.Id && (c.StartDate != null || c.DueDate != null)));
        var total = await query.CountAsync(ct);
        var items = await Read(query.OrderBy(w => w.Number).Skip(page.Skip).Take(page.PageSize), ct);
        return new Page<ItemData>(items, total, page.Page, page.PageSize);
    }

    /// <summary>Stages in stage order; epics by first date, due date and key, undated ones last (FR-042).</summary>
    private static IEnumerable<ItemData> OrderContainers(IEnumerable<ItemData> containers) => containers
        .OrderBy(c => c.Type == WorkItemType.Stage ? 0 : 1)
        .ThenBy(c => c.Type == WorkItemType.Stage ? c.Rank : "", StringComparer.Ordinal)
        .ThenBy(c => c.Type == WorkItemType.Stage ? DateOnly.MinValue : FirstDate(c) ?? DateOnly.MaxValue)
        .ThenBy(c => c.Type == WorkItemType.Stage ? DateOnly.MinValue : c.DueDate ?? DateOnly.MaxValue)
        .ThenBy(c => c.Type == WorkItemType.Stage ? c.Id : c.Number);

    private async Task<AppError> ConflictAsync(long id, string key, CancellationToken ct)
    {
        var current = await CurrentAsync(id, ct);
        return current.IsSuccess
            ? AppError.Conflict($"{key} was changed by someone else. The timeline now shows its current dates.", current.Value)
            : current.Error!;
    }

    private async Task<Result<TimelineItem>> CurrentAsync(long id, CancellationToken ct)
    {
        var data = (await Read(db.WorkItems.AsNoTracking().Where(w => w.Id == id), ct)).SingleOrDefault();
        if (data is null)
        {
            return AppError.NotFound("task");
        }

        var info = await workflow.GetBoardInfoAsync(data.ProjectId, ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(data.ProjectId, ct),
            data.AssigneeId is { } assigneeId ? [assigneeId] : [], ct);
        return data.ToItem(info.Statuses, DoneIds(info), people);
    }

    private static Task<List<ItemData>> Read(IQueryable<WorkItem> items, CancellationToken ct) =>
        items.Select(w => new ItemData(w.Id, w.ProjectId, w.Number, w.Key, w.Title, w.ParentId, w.StatusId, w.AssigneeId, w.StartDate,
            w.DueDate, w.RowVersion, w.Type, w.ContainerId, w.Rank, w.Color)).ToListAsync(ct);

    private static List<long> DoneIds(BoardInfo info) =>
        info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();

    private static DateOnly? FirstDate(ItemData item) => item.StartDate ?? item.DueDate;

    private sealed record ItemData(long Id, long ProjectId, int Number, string Key, string Title, long? ParentId, long StatusId,
        Guid? AssigneeId, DateOnly? StartDate, DateOnly? DueDate, byte[] Version, WorkItemType Type, long? ContainerId, string Rank,
        EpicColor? Color)
    {
        public bool IsScheduled => StartDate is not null || DueDate is not null;

        public TimelineItem ToItem(IReadOnlyList<StatusInfo> statuses, List<long> doneIds, IReadOnlyDictionary<Guid, AssigneeRef> people) =>
            new(Key, Title, WorkItemReads.ToOption(statuses, StatusId), AssigneeId is { } id ? people[id] : null, StartDate, DueDate,
                !doneIds.Contains(StatusId), Version)
            {
                Type = Type,
                Color = Color,
            };
    }
}
