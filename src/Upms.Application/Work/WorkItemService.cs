using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The details drawer (FR-023 to FR-033).</summary>
internal sealed class WorkItemService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IWorkItemNumberAllocator numbers,
    IUserDirectory users,
    IProjectTeam team,
    AssigneeReads assignees,
    CardRanker ranker,
    WorkItemReads reads,
    TimeProvider time) : IWorkItemService
{
    public async Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        return allowed.IsSuccess ? await DetailsAsync(item, allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit, byte[] expectedVersion,
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
            return await ConflictAsync(item, allowed.Value!, ct);
        }

        var context = ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow());
        IReadOnlyList<string> warnings = [];
        switch (edit)
        {
            case WorkItemEdit.Title title when item.Rename(title.Value, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Description description when item.Describe(description.Value, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Priority priority:
                item.Prioritize(priority.Value, context);
                break;
            case WorkItemEdit.Assignee assignee:
                if (assignee.UserId is { } userId && userId != item.AssigneeId
                    && !await team.CanBeAssignedAsync(item.ProjectId, userId, ct))
                {
                    return AppError.Rule(ErrorCodes.NotAssignable,
                        "That person can't be assigned: only active Project Admins and Members of the project can. The list of people has been updated.");
                }

                var names = await users.GetAsync(new[] { item.AssigneeId, assignee.UserId }.OfType<Guid>().Distinct().ToList(), ct);
                item.Assign(Person(item.AssigneeId, names), Person(assignee.UserId, names), context);
                break;
            case WorkItemEdit.Dates dates when item.Schedule(dates.Start, dates.Due, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Status status:
                var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
                var target = statuses.FirstOrDefault(s => s.Id == status.ColumnId);
                if (target is null)
                {
                    return AppError.NotFound("column");
                }

                var current = statuses.Single(s => s.Id == item.StatusId);
                if (target.Id != current.Id)
                {
                    // A task goes to the bottom of its new column; a sub-task keeps its place in its list.
                    var rank = item.ParentId is null
                        ? await ranker.AtEdgeAsync(item.ProjectId, target.Id, item.Id, atTop: false, ct)
                        : item.Rank;
                    item.Move(ToRef(current), ToRef(target), rank, null, null, null, context);
                    if (target.Category == StatusCategory.Done && current.Category != StatusCategory.Done)
                    {
                        warnings = await OpenSubtaskWarningAsync(item, statuses, ct);
                    }
                }

                break;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone else saved first, or the chosen column was deleted a moment ago.
            return await ConflictAsync(item, allowed.Value, ct);
        }

        return Result<WorkItemDetails>.Ok(await DetailsAsync(item, allowed.Value, ct), warnings);
    }

    public async Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title, CancellationToken ct)
    {
        var parent = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == parentKey, ct);
        if (parent is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(parent.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (parent.Type != WorkItemType.Task)
        {
            return AppError.Rule(ErrorCodes.SubtaskDepth, "A sub-task cannot have sub-tasks of its own.");
        }

        if (WorkItem.ValidateTitle(title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var firstToDo = await workflow.FirstToDoStatusAsync(parent.ProjectId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var number = await numbers.NextAsync(parent.ProjectId, ct);
        var rank = await ranker.AfterLastSiblingAsync(parent.Id, ct);
        var created = parent.AddSubtask(number, title, ToRef(firstToDo), rank,
            ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.WorkItems.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailsAsync(parent, allowed.Value, ct);
    }

    public async Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == subtaskKey, ct);
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
            return await ConflictAsync(item, allowed.Value!, ct);
        }

        var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
        var current = statuses.Single(s => s.Id == item.StatusId);
        if (current.Category != StatusCategory.Done)
        {
            // "Mark done" moves the sub-task to the leftmost "done" column (FR-028).
            var done = statuses.First(s => s.Category == StatusCategory.Done);
            item.Move(ToRef(current), ToRef(done), item.Rank, null, null, null,
                ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return await ConflictAsync(item, allowed.Value, ct);
            }
        }

        var shown = item.ParentId is { } parentId
            ? await db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == parentId, ct)
            : item;
        return await DetailsAsync(shown, allowed.Value!, ct);
    }

    public async Task<Result<DeletePreview>> PreviewDeleteAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (!await access.CanDeleteWorkItemAsync(item.ProjectId, item.CreatedById, ct))
        {
            return AppError.Forbidden("Only the task's creator, a Project Admin or an administrator can delete it.");
        }

        return new DeletePreview(item.Key, item.Title, await db.WorkItems.CountAsync(w => w.ParentId == item.Id, ct));
    }

    public async Task<Result> DeleteAsync(string workItemKey, CancellationToken ct)
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

        if (!await access.CanDeleteWorkItemAsync(item.ProjectId, item.CreatedById, ct))
        {
            return AppError.Forbidden("Only the task's creator, a Project Admin or an administrator can delete it.");
        }

        var subtasks = await db.WorkItems.Where(w => w.ParentId == item.Id).ToListAsync(ct);
        item.Delete(subtasks, ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Restore, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        page = page.Normalized();
        var projectId = allowed.Value!.ProjectId;
        var all = db.WorkItems.IgnoreQueryFilters().AsNoTracking();
        // Deleted tasks, and sub-tasks deleted on their own (not together with a deleted parent).
        var query = all.Where(w => w.ProjectId == projectId && w.IsDeleted
            && (w.ParentId == null || !all.Any(p => p.Id == w.ParentId && p.IsDeleted)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(w => w.DeletedAt).ThenBy(w => w.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(w => new
            {
                w.Key,
                w.Title,
                w.DeletedAt,
                w.DeletedById,
                Subtasks = all.Count(c => c.ParentId == w.Id && c.IsDeleted && c.DeletedAt == w.DeletedAt),
            })
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Where(r => r.DeletedById != null).Select(r => r.DeletedById!.Value).Distinct().ToList(), ct);
        var items = rows.ConvertAll(r => new DeletedItemView(r.Key, r.Title, r.DeletedAt!.Value,
            r.DeletedById is { } by ? WorkItemReads.NameOf(names, by) : "Unknown user", r.Subtasks));
        return new Page<DeletedItemView>(items, total, page.Page, page.PageSize);
    }

    public async Task<Result> RestoreAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.IgnoreQueryFilters().SingleOrDefaultAsync(w => w.Key == workItemKey && w.IsDeleted, ct);
        if (item is null)
        {
            return AppError.NotFound("deleted task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Restore, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (item.ParentId is { } parentId
            && await db.WorkItems.IgnoreQueryFilters().AnyAsync(p => p.Id == parentId && p.IsDeleted, ct))
        {
            return AppError.Rule("ParentDeleted", "Restore its parent task first; its sub-tasks come back with it.");
        }

        var subtasks = await db.WorkItems.IgnoreQueryFilters().Where(w => w.ParentId == item.Id).ToListAsync(ct);
        item.Restore(subtasks, ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<Page<SubtaskView>>> ListSubtasksAsync(string parentKey, PageRequest page, CancellationToken ct)
    {
        var parent = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == parentKey, ct);
        if (parent is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(parent.ProjectId, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        return await reads.SubtasksAsync(parent.Id, await workflow.StatusesAsync(parent.ProjectId, ct),
            await assignees.TeamAsync(parent.ProjectId, ct), page, ct);
    }

    public async Task<Result<Page<ChangeView>>> GetHistoryAsync(string workItemKey, PageRequest page, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        return allowed.IsSuccess ? await reads.HistoryAsync(item.Id, page, ct) : allowed.Error!;
    }

    private async Task<WorkItemDetails> DetailsAsync(WorkItem item, ProjectAccessInfo allowed, CancellationToken ct)
    {
        var info = await workflow.GetBoardInfoAsync(item.ProjectId, ct);
        var parent = item.ParentId is { } parentId
            ? await db.WorkItems.AsNoTracking().Where(w => w.Id == parentId).Select(w => new ParentRef(w.Key, w.Title)).SingleOrDefaultAsync(ct)
            : null;
        var creator = await users.GetAsync([item.CreatedById], ct);
        var members = await assignees.TeamAsync(item.ProjectId, ct);
        var assignee = item.AssigneeId is { } assigneeId
            ? (await assignees.DescribeAsync(members, [assigneeId], ct))[assigneeId]
            : null;
        return new WorkItemDetails(
            item.Key,
            info.Key,
            info.Name,
            item.Type,
            item.Title,
            item.Description,
            item.Priority,
            WorkItemReads.ToOption(info.Statuses, item.StatusId),
            info.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(),
            parent,
            WorkItemReads.NameOf(creator, item.CreatedById),
            item.CreatedAt,
            item.UpdatedAt,
            item.ResolvedAt,
            await access.CanDeleteWorkItemAsync(item.ProjectId, item.CreatedById, ct),
            item.RowVersion,
            await reads.SubtasksAsync(item.Id, info.Statuses, members, PageRequest.First, ct),
            await reads.CommentsAsync(item.Id, allowed.UserId, PageRequest.First, ct),
            await reads.HistoryAsync(item.Id, PageRequest.First, ct),
            assignee,
            item.StartDate,
            item.DueDate,
            AssigneeReads.Options(members, allowed.UserId, allowed.CanContribute),
            allowed.CanContribute);
    }

    private async Task<AppError> ConflictAsync(WorkItem item, ProjectAccessInfo allowed, CancellationToken ct)
    {
        var latest = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Id == item.Id, ct);
        return latest is null
            ? AppError.NotFound("task")
            : AppError.Conflict($"{item.Key} was changed by someone else. Review the latest values; your text is kept.",
                await DetailsAsync(latest, allowed, ct));
    }

    private async Task<IReadOnlyList<string>> OpenSubtaskWarningAsync(WorkItem item, IReadOnlyList<StatusInfo> statuses,
        CancellationToken ct)
    {
        var doneIds = statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var open = await db.WorkItems.AsNoTracking()
            .Where(c => c.ParentId == item.Id && !doneIds.Contains(c.StatusId))
            .OrderBy(c => c.Rank)
            .Select(c => c.Key)
            .ToListAsync(ct);
        return open.Count == 0
            ? []
            : [$"{item.Key} is done but {open.Count} sub-task{(open.Count == 1 ? " is" : "s are")} still open: {string.Join(", ", open)}."];
    }

    private static StatusRef ToRef(StatusInfo status) => new(status.Id, status.Name, status.Category);

    private static PersonRef? Person(Guid? userId, IReadOnlyDictionary<Guid, UserDisplay> names) =>
        userId is { } id ? new PersonRef(id, WorkItemReads.NameOf(names, id)) : null;
}
