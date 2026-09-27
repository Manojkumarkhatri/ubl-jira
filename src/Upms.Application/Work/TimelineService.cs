using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reads a project's scheduled items from <c>IX_WorkItems_Project_Live</c> and groups sub-tasks under their
/// task; rescheduling goes through <see cref="WorkItem.Schedule"/>, like the drawer's dates (Phase 2 research R8, R12).</summary>
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

        var scheduled = await Read(live.Where(w => w.StartDate != null || w.DueDate != null), ct);
        var scheduledSubtasks = scheduled.Where(i => i.ParentId != null).ToLookup(i => i.ParentId!.Value);
        // Tasks without dates of their own that have scheduled sub-tasks head a row too.
        var headIds = scheduledSubtasks.Select(g => g.Key).Except(scheduled.Where(i => i.ParentId == null).Select(i => i.Id)).ToList();
        var heads = scheduled.Where(i => i.ParentId == null).Concat(await Read(live.Where(w => headIds.Contains(w.Id)), ct)).ToList();
        var ordered = heads
            .Select(head => (Head: head, Subtasks: scheduledSubtasks[head.Id].OrderBy(FirstDate).ThenBy(i => i.DueDate).ThenBy(i => i.Number).ToList()))
            .OrderBy(row => FirstDate(row.Head) ?? row.Subtasks.Min(FirstDate))
            .ThenBy(row => row.Head.DueDate ?? DateOnly.MaxValue)
            .ThenBy(row => row.Head.Number)
            .ToList();
        var shown = ordered.Skip(Math.Max(0, ordered.Count - TimelineView.MaxRows)).ToList();

        var shownIds = shown.Select(r => r.Head.Id).ToList();
        var unscheduledSubtasks = (await Read(live.Where(w => w.ParentId != null && shownIds.Contains(w.ParentId.Value)
                && w.StartDate == null && w.DueDate == null), ct))
            .ToLookup(i => i.ParentId!.Value);
        var unscheduled = await UnscheduledAsync(info.ProjectId, hideCompleted, doneIds, PageRequest.First, ct);

        var team = await assignees.TeamAsync(info.ProjectId, ct);
        var people = await assignees.DescribeAsync(team,
            shown.SelectMany(r => r.Subtasks.Prepend(r.Head))
                .Concat(shown.SelectMany(r => unscheduledSubtasks[r.Head.Id]))
                .Concat(unscheduled.Items)
                .Where(i => i.AssigneeId != null)
                .Select(i => i.AssigneeId!.Value),
            ct);
        TimelineItem ToItem(ItemData data) => data.ToItem(info.Statuses, doneIds, people);

        var rows = shown.ConvertAll(r => new TimelineRow(
            ToItem(r.Head),
            r.Subtasks.ConvertAll(ToItem),
            unscheduledSubtasks[r.Head.Id].OrderBy(i => i.Number).Select(ToItem).ToList()));
        return new TimelineView(info.Key, info.Name, caller.CanContribute, caller.CanManage, caller.IsAdministrator, rows,
            ordered.Count, new Page<TimelineItem>(unscheduled.Items.Select(ToItem).ToList(), unscheduled.TotalCount, unscheduled.PageNumber,
                unscheduled.PageSize), hideCompleted);
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

    private async Task<Page<ItemData>> UnscheduledAsync(long projectId, bool hideCompleted, List<long> doneIds, PageRequest page,
        CancellationToken ct)
    {
        page = new PageRequest(page.Page, UnscheduledPageSize).Normalized();
        var all = db.WorkItems.AsNoTracking();
        var query = Live(projectId, hideCompleted, doneIds)
            .Where(w => w.ParentId == null && w.StartDate == null && w.DueDate == null
                && !all.Any(c => c.ParentId == w.Id && (c.StartDate != null || c.DueDate != null)));
        var total = await query.CountAsync(ct);
        var items = await Read(query.OrderBy(w => w.Number).Skip(page.Skip).Take(page.PageSize), ct);
        return new Page<ItemData>(items, total, page.Page, page.PageSize);
    }

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
            w.DueDate, w.RowVersion)).ToListAsync(ct);

    private static List<long> DoneIds(BoardInfo info) =>
        info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();

    private static DateOnly? FirstDate(ItemData item) => item.StartDate ?? item.DueDate;

    private sealed record ItemData(long Id, long ProjectId, int Number, string Key, string Title, long? ParentId, long StatusId,
        Guid? AssigneeId, DateOnly? StartDate, DateOnly? DueDate, byte[] Version)
    {
        public TimelineItem ToItem(IReadOnlyList<StatusInfo> statuses, List<long> doneIds, IReadOnlyDictionary<Guid, AssigneeRef> people) =>
            new(Key, Title, WorkItemReads.ToOption(statuses, StatusId), AssigneeId is { } id ? people[id] : null, StartDate, DueDate,
                !doneIds.Contains(StatusId), Version);
    }
}
