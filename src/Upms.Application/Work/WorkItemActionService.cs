using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Jira's "⋯" menu on a work item: log work, vote, select a cover, clone and move.</summary>
internal sealed class WorkItemActionService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IWorkItemNumberAllocator numbers,
    IUserDirectory users,
    IProjectTeam team,
    CardRanker ranker,
    SprintReads sprintReads,
    StageLocks locks,
    DependencyReads dependencies,
    WatchList watchers,
    SearchIndexer indexer,
    IAttachmentContentStore contents,
    TimeProvider time) : IWorkItemActionService
{
    /// <summary>The work logs a drawer shows; Jira pages them too.</summary>
    public const int MaxLogs = 200;

    /// <summary>The child items a clone copies at most, so one clone stays one quick change.</summary>
    public const int MaxClonedChildren = 100;

    private static readonly WorkItemType[] Movable =
        [WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask];

    // ----- Time tracking -----

    public async Task<Result<TimeTracking>> GetTimeAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        return allowed.IsSuccess ? await TimeOfAsync(item, allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<TimeTracking>> LogWorkAsync(string workItemKey, WorkLogInput input, CancellationToken ct)
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

        var caller = allowed.Value!;
        if (!WorkDuration.TryParse(input.TimeSpent, out var minutes))
        {
            return InvalidDuration("TimeSpent", "Enter the time spent, such as 2h 30m or 1d.");
        }

        if (ParseRemaining(input.Remaining, out var remaining) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        var log = WorkLog.Create(item.Id, caller.UserId, input.StartedAt, minutes, input.Note, now);
        if (!log.IsSuccess)
        {
            return log.Error!.ToAppError();
        }

        item.LogWork(minutes, remaining, ChangeContext.New(caller.UserId, now));
        db.WorkLogs.Add(log.Value!);
        return await SaveTimeAsync(item, caller, ct);
    }

    public async Task<Result<TimeTracking>> ChangeWorkLogAsync(long workLogId, WorkLogInput input, CancellationToken ct)
    {
        var found = await LogForChangeAsync(workLogId, ct);
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        var (log, item, caller) = found.Value!;
        if (!WorkDuration.TryParse(input.TimeSpent, out var minutes))
        {
            return InvalidDuration("TimeSpent", "Enter the time spent, such as 2h 30m or 1d.");
        }

        if (ParseRemaining(input.Remaining, out var remaining) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        var context = ChangeContext.New(caller.UserId, now);
        var before = log.Minutes;
        if (log.Change(input.StartedAt, minutes, input.Note, now) is { } refused)
        {
            return refused.ToAppError();
        }

        item.ChangeLoggedWork(before, minutes, context);
        if (remaining is not null)
        {
            item.SetRemainingEstimate(remaining, context);
        }

        return await SaveTimeAsync(item, caller, ct);
    }

    public async Task<Result<TimeTracking>> DeleteWorkLogAsync(long workLogId, CancellationToken ct)
    {
        var found = await LogForChangeAsync(workLogId, ct);
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        var (log, item, caller) = found.Value!;
        item.RemoveLoggedWork(log.Minutes, ChangeContext.New(caller.UserId, time.GetUtcNow()));
        db.WorkLogs.Remove(log);
        return await SaveTimeAsync(item, caller, ct);
    }

    public Task<Result<TimeTracking>> SetOriginalEstimateAsync(string workItemKey, string? text, CancellationToken ct) =>
        SetEstimateAsync(workItemKey, text, original: true, ct);

    public Task<Result<TimeTracking>> SetRemainingEstimateAsync(string workItemKey, string? text, CancellationToken ct) =>
        SetEstimateAsync(workItemKey, text, original: false, ct);

    private async Task<Result<TimeTracking>> SetEstimateAsync(string workItemKey, string? text, bool original, CancellationToken ct)
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

        if (ParseRemaining(text, out var minutes, original ? "OriginalEstimate" : "RemainingEstimate") is { } invalid)
        {
            return invalid;
        }

        var context = ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow());
        if (original)
        {
            item.SetOriginalEstimate(minutes, context);
        }
        else
        {
            item.SetRemainingEstimate(minutes, context);
        }

        return await SaveTimeAsync(item, allowed.Value, ct);
    }

    private async Task<Result<(WorkLog Log, WorkItem Item, ProjectAccessInfo Caller)>> LogForChangeAsync(long workLogId, CancellationToken ct)
    {
        var log = await db.WorkLogs.SingleOrDefaultAsync(l => l.Id == workLogId, ct);
        var item = log is null ? null : await db.WorkItems.SingleOrDefaultAsync(w => w.Id == log.WorkItemId, ct);
        if (log is null || item is null)
        {
            return AppError.NotFound("work log");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        return CanChange(log, allowed.Value!)
            ? (log, item, allowed.Value!)
            : AppError.Forbidden("Only the person who logged the time, someone who manages the space or an Admin can change it.");
    }

    private async Task<Result<TimeTracking>> SaveTimeAsync(WorkItem item, ProjectAccessInfo caller, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.Conflict($"{item.Key} was changed by someone else at the same moment. Try again.");
        }

        return await TimeOfAsync(item, caller, ct);
    }

    private async Task<TimeTracking> TimeOfAsync(WorkItem item, ProjectAccessInfo caller, CancellationToken ct)
    {
        var logs = await db.WorkLogs.AsNoTracking()
            .Where(l => l.WorkItemId == item.Id)
            .OrderByDescending(l => l.StartedAt).ThenByDescending(l => l.Id)
            .Take(MaxLogs)
            .ToListAsync(ct);
        var names = await users.GetAsync(logs.Select(l => l.AuthorId).Distinct().ToList(), ct);
        return new TimeTracking(item.Key, item.OriginalEstimateMinutes, item.RemainingEstimateMinutes, item.TimeSpentMinutes,
            logs.ConvertAll(l => new WorkLogView(l.Id, l.AuthorId, WorkItemReads.NameOf(names, l.AuthorId), l.StartedAt, l.Minutes,
                l.Note, l.CreatedAt, CanChange(l, caller))),
            caller.CanContribute);
    }

    private static bool CanChange(WorkLog log, ProjectAccessInfo caller) =>
        caller.CanManage || caller.IsAdministrator || (caller.CanContribute && log.AuthorId == caller.UserId);

    /// <summary>An estimate as typed: empty for none, "0" for nothing left, otherwise a duration.</summary>
    private static AppError? ParseRemaining(string? text, out int? minutes, string field = "Remaining")
    {
        minutes = null;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.TrimEnd('w', 'd', 'h', 'm', 'W', 'D', 'H', 'M', ' ') is "0" or "0.0")
        {
            minutes = 0;
            return null;
        }

        if (!WorkDuration.TryParse(trimmed, out var parsed))
        {
            return InvalidDuration(field, "Write the time as 2w 4d 6h 45m: w weeks, d days, h hours, m minutes.");
        }

        minutes = parsed;
        return null;
    }

    private static AppError InvalidDuration(string field, string message) =>
        new(ErrorKind.Validation, WorkDuration.InvalidCode, message, new Dictionary<string, string[]> { [field] = [message] });

    // ----- Votes -----

    public async Task<Result<VoteState>> ToggleVoteAsync(string workItemKey, CancellationToken ct)
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

        var userId = allowed.Value!.UserId;
        var mine = await db.WorkItemVotes.SingleOrDefaultAsync(v => v.WorkItemId == item.Id && v.UserId == userId, ct);
        if (mine is not null)
        {
            db.WorkItemVotes.Remove(mine);
        }
        else if (VoteRefusal(item, userId) is { } refusal)
        {
            return AppError.Rule("VoteNotAllowed", refusal);
        }
        else
        {
            db.WorkItemVotes.Add(WorkItemVote.Create(item.Id, userId, time.GetUtcNow()));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Voted in two windows at once: the vote is there either way.
        }

        return new VoteState(await db.WorkItemVotes.CountAsync(v => v.WorkItemId == item.Id, ct), mine is null);
    }

    /// <summary>As in Jira: people vote for work others reported, while it is open.</summary>
    public static string? VoteRefusal(WorkItem item, Guid userId) =>
        item.CreatedById == userId ? "You can't vote for work you created."
        : item.ResolvedAt is not null ? "You can't vote for work that is done."
        : null;

    // ----- Cover -----

    public async Task<Result> SetCoverAsync(string workItemKey, string? color, long? attachmentId, CancellationToken ct)
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

        if (color is not null && (attachmentId is not null || !CoverColors.All.Contains(color)))
        {
            return AppError.Validation("Cover", "Choose one of the colours, or an image.");
        }

        if (attachmentId is { } fileId)
        {
            var file = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == fileId && a.WorkItemId == item.Id, ct);
            if (file is not { IsAvailable: true, IsImage: true })
            {
                return AppError.Validation("Cover", "Choose an image attached to this item that has passed its check.");
            }
        }

        item.SetCover(color, attachmentId);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.Conflict($"{item.Key} was changed by someone else at the same moment. Try again.");
        }

        return Result.Ok();
    }

    // ----- Clone -----

    public async Task<Result<string>> CloneAsync(string workItemKey, CloneRequest request, CancellationToken ct)
    {
        var source = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (source is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(source.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (source.Type == WorkItemType.Stage)
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, "A stage can't be cloned: add a new stage in Stages, with its own gate.");
        }

        if (source.Type != WorkItemType.Subtask && WorkItemTypes.RefusalFor(source.Type, caller.Template, caller.WorkTypes) is { } refusal)
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, refusal);
        }

        if (WorkItem.ValidateTitle(request.Title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var statuses = await workflow.StatusesAsync(caller.ProjectId, ct);
        var firstToDo = ToRef(statuses.First(s => s.Category == StatusCategory.ToDo));
        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var parent = source.ParentId is { } parentId ? await db.WorkItems.SingleOrDefaultAsync(w => w.Id == parentId, ct) : null;
        var created = await NewCopyAsync(source, request.Title, parent, firstToDo, caller, context, ct);
        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        var clone = created.Value!;
        if (source.ContainerId is { } containerId
            && await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Id == containerId, ct) is { } container)
        {
            clone.PlaceIn(null, RefOf(container), context);
        }

        if (source.Color is { } color && source.Type == WorkItemType.Epic)
        {
            clone.SetColor(color, context);
        }

        if (request.Sprint && source.SprintId is { } sprintId
            && await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct) is { State: not SprintState.Completed } sprint
            && clone.JoinSprint(null, sprint.Ref, context) is null && sprint.State == SprintState.Active)
        {
            sprintReads.Record(sprint, clone, SprintScopeKind.Added, context);
        }

        clone.SetCover(source.CoverColor, null);
        await db.SaveChangesAsync(ct);

        await CopyLabelsAsync(source.Id, clone.Id, caller.UserId, context.At, ct);
        if (request.Attachments)
        {
            await CopyAttachmentsAsync(source.Id, clone.Id, caller.UserId, context.At, ct);
        }

        if (request.Links)
        {
            await CopyLinksAsync(source, clone, context, ct);
        }

        if (request.ChildItems)
        {
            if (await CopyChildrenAsync(source, clone, firstToDo, caller, context, ct) is { } refused)
            {
                return refused;
            }
        }

        clone.RecordActivity(WorkItemField.ClonedFrom, source.Key, null, context);
        source.RecordActivity(WorkItemField.ClonedTo, null, clone.Key, context);
        await watchers.AddAsync(clone.Id, [caller.UserId], ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return clone.Key;
    }

    /// <summary>A copy of the item's own fields in the first "to do" column, saved so it has an id: the description,
    /// priority, assignee (while they can still be assigned), dates, estimates and labels follow.</summary>
    private async Task<Result<WorkItem>> NewCopyAsync(WorkItem source, string title, WorkItem? parent, StatusRef status,
        ProjectAccessInfo caller, ChangeContext context, CancellationToken ct)
    {
        var number = await numbers.NextAsync(caller.ProjectId, ct);
        var rank = parent is not null ? await ranker.AfterLastSiblingAsync(parent.Id, ct)
            : WorkItemTypes.IsCard(source.Type) ? await ranker.ForNewCardAsync(caller.ProjectId, caller.Template, status.Id, ct)
            : Rank.First();
        var created = parent is not null
            ? parent.AddSubtask(number, title, status, rank, context)
            : WorkItem.Create(caller.ProjectId, caller.Key, number, source.Type, title, status, rank, caller.Template, context, caller.WorkTypes);
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var copy = created.Value!;
        copy.CopyDescription(source);
        if (source.Priority != copy.Priority)
        {
            copy.Prioritize(source.Priority, context);
        }

        if (source.AssigneeId is { } assigneeId && await team.CanBeAssignedAsync(caller.ProjectId, assigneeId, ct))
        {
            copy.Assign(null, ItemEdits.Person(assigneeId, await users.GetAsync([assigneeId], ct)), context);
        }

        if (source.StartDate is not null || source.DueDate is not null)
        {
            copy.Schedule(source.StartDate, source.DueDate, context);
        }

        if (source.Estimate is not null && WorkItemTypes.HasEstimate(copy.Type, caller.Template))
        {
            copy.SetEstimate(source.Estimate, caller.Template, context);
        }

        copy.SetOriginalEstimate(source.OriginalEstimateMinutes, context);
        db.WorkItems.Add(copy);
        await db.SaveChangesAsync(ct);
        await indexer.IndexAsync(copy, ct);
        return copy;
    }

    private async Task CopyLabelsAsync(long sourceId, long copyId, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var labelId in await db.WorkItemLabels.Where(l => l.WorkItemId == sourceId).Select(l => l.LabelId).ToListAsync(ct))
        {
            db.WorkItemLabels.Add(WorkItemLabel.Add(copyId, labelId, userId, now));
        }
    }

    /// <summary>Copies the files still listed on the item; a blocked file is never copied, and one still being checked is
    /// checked again.</summary>
    private async Task CopyAttachmentsAsync(long sourceId, long copyId, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var files = await db.Attachments.AsNoTracking()
            .Where(a => a.WorkItemId == sourceId && a.RemovedAt == null && a.PurgedAt == null && a.CheckStatus != AttachmentCheckStatus.Blocked)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
        foreach (var file in files)
        {
            var copy = Attachment.CopyOf(file, copyId, userId, now);
            db.Attachments.Add(copy);
            await db.SaveChangesAsync(ct);

            // Read the bytes in full before writing them back: one connection cannot stream both ways at once.
            using var buffer = new MemoryStream();
            await using (var content = await contents.OpenReadAsync(file.Id, ct))
            {
                if (content is null)
                {
                    db.Attachments.Remove(copy);
                    continue;
                }

                await content.CopyToAsync(buffer, ct);
            }

            buffer.Position = 0;
            await contents.WriteAsync(copy.Id, buffer, ct);
        }
    }

    /// <summary>The clone waits for, and is waited for by, the same items as the original. The links of a new item cannot
    /// make a loop: any loop through the clone would run through the original too.</summary>
    private async Task CopyLinksAsync(WorkItem source, WorkItem clone, ChangeContext context, CancellationToken ct)
    {
        var links = await db.Dependencies.AsNoTracking()
            .Where(d => d.PredecessorId == source.Id || d.SuccessorId == source.Id)
            .ToListAsync(ct);
        var otherIds = links.Select(l => l.PredecessorId == source.Id ? l.SuccessorId : l.PredecessorId).Distinct().ToList();
        var others = await db.WorkItems.Where(w => otherIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        foreach (var link in links)
        {
            if (link.SuccessorId == source.Id && others.TryGetValue(link.PredecessorId, out var waitedFor))
            {
                db.Dependencies.Add(Dependency.Create(clone.ProjectId, waitedFor.Id, clone.Id, context));
                clone.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitsForText(waitedFor), context);
                waitedFor.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitedForByText(clone), context);
            }
            else if (link.PredecessorId == source.Id && others.TryGetValue(link.SuccessorId, out var waiting))
            {
                db.Dependencies.Add(Dependency.Create(clone.ProjectId, clone.Id, waiting.Id, context));
                clone.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitedForByText(waiting), context);
                waiting.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitsForText(clone), context);
            }
        }
    }

    /// <summary>A story's, task's or bug's sub-tasks become the clone's; an epic's items are copied into the new epic.</summary>
    private async Task<AppError?> CopyChildrenAsync(WorkItem source, WorkItem clone, StatusRef firstToDo, ProjectAccessInfo caller,
        ChangeContext context, CancellationToken ct)
    {
        var isEpic = source.Type == WorkItemType.Epic;
        if (!isEpic && !WorkItemTypes.IsCard(source.Type))
        {
            return null;
        }

        var children = await db.WorkItems.AsNoTracking()
            .Where(w => isEpic ? w.ContainerId == source.Id : w.ParentId == source.Id)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Take(MaxClonedChildren + 1)
            .ToListAsync(ct);
        if (children.Count > MaxClonedChildren)
        {
            return AppError.Rule("TooManyChildren",
                $"{source.Key} has more than {MaxClonedChildren} child items. Clone it without them, then move the ones you need.");
        }

        var cloneRef = RefOf(clone);
        foreach (var child in children)
        {
            if (isEpic && WorkItemTypes.RefusalFor(child.Type, caller.Template, caller.WorkTypes) is not null)
            {
                continue; // a type the space no longer uses
            }

            var copied = await NewCopyAsync(child, child.Title, isEpic ? null : clone, firstToDo, caller, context, ct);
            if (!copied.IsSuccess)
            {
                return copied.Error;
            }

            if (isEpic)
            {
                copied.Value!.PlaceIn(null, cloneRef, context);
            }

            await CopyLabelsAsync(child.Id, copied.Value!.Id, caller.UserId, context.At, ct);
            await db.SaveChangesAsync(ct);
        }

        return null;
    }

    // ----- Move -----

    public async Task<Result<MovePreview>> PreviewMoveAsync(string workItemKey, MoveTarget request, CancellationToken ct)
    {
        var planned = await PlanAsync(workItemKey, request, ct);
        if (!planned.IsSuccess)
        {
            return planned.Error!;
        }

        var plan = planned.Value!;
        var item = plan.Item;
        var oldParent = item.ParentId is { } parentId
            ? await db.WorkItems.AsNoTracking().Where(w => w.Id == parentId).Select(w => w.Key).SingleOrDefaultAsync(ct)
            : null;
        return new MovePreview(item.Key, item.Title, plan.FromName, plan.ToName, item.Type, request.Type, oldParent, plan.Parent?.Key,
            plan.Current.Name, plan.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(), plan.Target.Id,
            await ConsequencesAsync(plan, request.Type, ct));
    }

    public async Task<Result<string>> MoveAsync(string workItemKey, MoveTarget request, CancellationToken ct)
    {
        var planned = await PlanAsync(workItemKey, request, ct);
        if (!planned.IsSuccess)
        {
            return planned.Error!;
        }

        var plan = planned.Value!;
        var item = plan.Item;
        var type = request.Type;
        var context = ChangeContext.New(plan.Source.UserId, time.GetUtcNow());
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // What the item cannot keep: its sprint, its epic or stage, an epic's items, its story points.
        if (item.SprintId is { } sprintId && (plan.IsOtherSpace || type == WorkItemType.Subtask || !WorkItemTypes.IsCard(type))
            && await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct) is { State: not SprintState.Completed } sprint
            && item.JoinSprint(sprint.Ref, null, context) is null && sprint.State == SprintState.Active)
        {
            sprintReads.Record(sprint, item, SprintScopeKind.Removed, context);
        }

        if (item.ContainerId is { } containerId && (plan.IsOtherSpace || WorkItemTypes.LevelOf(type) != WorkItemLevel.Standard)
            && await db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(w => w.Id == containerId, ct) is { } container)
        {
            item.PlaceIn(RefOf(container), null, context);
        }

        if (item.Type == WorkItemType.Epic && (type != WorkItemType.Epic || plan.IsOtherSpace))
        {
            var self = RefOf(item);
            foreach (var contained in await db.WorkItems.Where(w => w.ContainerId == item.Id).ToListAsync(ct))
            {
                contained.PlaceIn(self, null, context);
            }
        }

        if (item.Estimate is not null && WorkItemTypes.HasEstimate(item.Type, plan.Source.Template)
            && !WorkItemTypes.HasEstimate(type, plan.Destination.Template))
        {
            item.SetEstimate(null, plan.Source.Template, context);
        }

        // Another space: dependencies link items of one space, and the assignee must be able to work there.
        if (plan.IsOtherSpace)
        {
            await dependencies.RemoveLinksAsync([item, .. plan.Subtasks], context, ct, "moved");
            var people = await users.GetAsync(
                [.. new[] { item.AssigneeId }.Concat(plan.Subtasks.Select(s => s.AssigneeId)).OfType<Guid>().Distinct()], ct);
            foreach (var moving in plan.Subtasks.Prepend(item))
            {
                if (moving.AssigneeId is { } assigneeId && !await team.CanBeAssignedAsync(plan.Destination.ProjectId, assigneeId, ct))
                {
                    moving.Assign(ItemEdits.Person(assigneeId, people), null, context);
                }
            }
        }

        var rank = type == WorkItemType.Subtask ? await ranker.AfterLastSiblingAsync(plan.Parent!.Id, ct)
            : !WorkItemTypes.IsCard(type) ? Rank.First()
            : plan.IsOtherSpace || !WorkItemTypes.IsCard(item.Type) || item.ParentId is not null
                ? await ranker.ForNewCardAsync(plan.Destination.ProjectId, plan.Destination.Template, plan.Target.Id, ct)
                : item.Rank;
        var oldParentKey = item.ParentId is { } oldParentId
            ? await db.WorkItems.AsNoTracking().Where(w => w.Id == oldParentId).Select(w => w.Key).SingleOrDefaultAsync(ct)
            : null;
        item.Convert(type, plan.Parent?.Id, oldParentKey, plan.Parent?.Key, context);
        if (plan.IsOtherSpace)
        {
            var movedIds = plan.Subtasks.Prepend(item).Select(w => w.Id).ToList();
            item.Relocate(plan.Destination.ProjectId, plan.Destination.Key, await numbers.NextAsync(plan.Destination.ProjectId, ct),
                ToRef(plan.Current), ToRef(plan.Target), rank, context);
            foreach (var subtask in plan.Subtasks)
            {
                var current = plan.SourceStatuses.Single(s => s.Id == subtask.StatusId);
                subtask.Relocate(plan.Destination.ProjectId, plan.Destination.Key, await numbers.NextAsync(plan.Destination.ProjectId, ct),
                    ToRef(current), ToRef(MapStatus(current, plan.Statuses)), subtask.Rank, context);
            }

            // The search words and notifications follow the items to their new space.
            await db.SearchTerms.Where(t => movedIds.Contains(t.WorkItemId))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ProjectId, plan.Destination.ProjectId), ct);
            await db.Notifications.Where(n => movedIds.Contains(n.WorkItemId))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ProjectId, plan.Destination.ProjectId), ct);
        }
        else
        {
            item.Rerank(rank);
        }

        plan.Parent?.RecordActivity(WorkItemField.SubtaskAdded, null, item.Key, context, item.Title);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.Conflict($"{workItemKey} was changed by someone else at the same moment. Try again.");
        }

        await transaction.CommitAsync(ct);
        return item.Key;
    }

    private sealed record MovePlan(
        WorkItem Item,
        ProjectAccessInfo Source,
        ProjectAccessInfo Destination,
        string FromName,
        string ToName,
        IReadOnlyList<StatusInfo> SourceStatuses,
        IReadOnlyList<StatusInfo> Statuses,
        StatusInfo Current,
        StatusInfo Target,
        WorkItem? Parent,
        IReadOnlyList<WorkItem> Subtasks)
    {
        public bool IsOtherSpace => Source.ProjectId != Destination.ProjectId;
    }

    /// <summary>Jira's move rules: epics, stories, tasks, bugs and sub-tasks change into one another; a sub-task needs a
    /// story, task or bug of the destination as its parent; an item with sub-tasks cannot become a sub-task or an epic;
    /// sub-tasks go with their parent to another space.</summary>
    private async Task<Result<MovePlan>> PlanAsync(string workItemKey, MoveTarget request, CancellationToken ct)
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

        var source = allowed.Value!;
        if (!Movable.Contains(item.Type))
        {
            return AppError.Rule("MoveNotAllowed", "Stages and milestones keep their place in the plan, so they can't be moved.");
        }

        if (!Movable.Contains(request.Type))
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, "Choose an epic, story, task, bug or sub-task.");
        }

        var destination = source;
        if (!string.Equals(request.SpaceKey, source.Key, StringComparison.OrdinalIgnoreCase))
        {
            var other = await access.RequireAsync(request.SpaceKey, ProjectRight.Contribute, ct);
            if (!other.IsSuccess)
            {
                return other.Error!;
            }

            destination = other.Value!;
        }

        if (request.Type != WorkItemType.Subtask && WorkItemTypes.RefusalFor(request.Type, destination.Template, destination.WorkTypes) is { } refusal)
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, refusal);
        }

        var subtasks = await db.WorkItems.Where(w => w.ParentId == item.Id).OrderBy(w => w.Rank).ToListAsync(ct);
        if (subtasks.Count > 0 && request.Type is WorkItemType.Subtask or WorkItemType.Epic)
        {
            return AppError.Rule("HasSubtasks",
                $"{item.Key} has {Count(subtasks.Count, "sub-task")}. Move them to another item or change their type first: " +
                $"{(request.Type == WorkItemType.Epic ? "an epic" : "a sub-task")} has no sub-tasks.");
        }

        WorkItem? parent = null;
        if (request.Type == WorkItemType.Subtask)
        {
            if (string.IsNullOrWhiteSpace(request.ParentKey))
            {
                return AppError.Rule(ErrorCodes.ParentRequired, "Choose the story, task or bug this sub-task belongs to.");
            }

            parent = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == request.ParentKey && w.ProjectId == destination.ProjectId, ct);
            if (parent is null || parent.Id == item.Id || parent.ParentId is not null || !WorkItemTypes.IsCard(parent.Type))
            {
                return AppError.Rule(ErrorCodes.InvalidParent, "Choose a story, task or bug of the destination space as the parent.");
            }
        }

        if (destination.ProjectId == source.ProjectId && request.Type == item.Type && parent?.Id == item.ParentId)
        {
            return AppError.Rule("NothingToMove", "Choose another space, work type or parent.");
        }

        var sourceStatuses = await workflow.StatusesAsync(source.ProjectId, ct);
        var current = sourceStatuses.Single(s => s.Id == item.StatusId);
        var statuses = destination.ProjectId == source.ProjectId ? sourceStatuses : await workflow.StatusesAsync(destination.ProjectId, ct);
        var target = current;
        if (destination.ProjectId != source.ProjectId)
        {
            target = request.StatusId is { } statusId ? statuses.FirstOrDefault(s => s.Id == statusId) : MapStatus(current, statuses);
            if (target is null)
            {
                return AppError.NotFound("column");
            }
        }

        // Started work cannot become a sub-task of work in a locked stage (Phase 3 FR-032, FR-033).
        if (parent?.ContainerId is { } lockedId && await locks.CheckPlaceInAsync(item, target.Category, lockedId, ct) is { } locked)
        {
            return locked;
        }

        var names = await workflow.GetBoardInfoAsync(source.ProjectId, ct);
        var toName = destination.ProjectId == source.ProjectId ? names.Name : (await workflow.GetBoardInfoAsync(destination.ProjectId, ct)).Name;
        return new MovePlan(item, source, destination, names.Name, toName, sourceStatuses, statuses, current, target, parent,
            destination.ProjectId == source.ProjectId ? [] : subtasks);
    }

    /// <summary>What the move takes away, in the confirmation's sentences.</summary>
    private async Task<IReadOnlyList<string>> ConsequencesAsync(MovePlan plan, WorkItemType type, CancellationToken ct)
    {
        var item = plan.Item;
        var lines = new List<string>();
        if (plan.IsOtherSpace)
        {
            lines.Add($"It gets a new key in {plan.ToName}; links to {item.Key} still find it.");
            if (plan.Subtasks.Count > 0)
            {
                lines.Add($"Its {Count(plan.Subtasks.Count, "sub-task")} move{(plan.Subtasks.Count == 1 ? "s" : "")} with it.");
            }
        }

        if (item.ParentId is not null && type != WorkItemType.Subtask)
        {
            lines.Add("It is no longer a sub-task.");
        }

        if (item.ContainerId is { } containerId && (plan.IsOtherSpace || WorkItemTypes.LevelOf(type) != WorkItemLevel.Standard)
            && await db.WorkItems.AsNoTracking().Where(w => w.Id == containerId).Select(w => w.Key).SingleOrDefaultAsync(ct) is { } containerKey)
        {
            lines.Add($"It leaves {containerKey}.");
        }

        if (item.Type == WorkItemType.Epic && (type != WorkItemType.Epic || plan.IsOtherSpace)
            && await db.WorkItems.CountAsync(w => w.ContainerId == item.Id, ct) is > 0 and var contained)
        {
            lines.Add($"Its {Count(contained, "item")} stay{(contained == 1 ? "s" : "")} in {plan.FromName}, in no epic.");
        }

        if (item.SprintId is { } sprintId && (plan.IsOtherSpace || type == WorkItemType.Subtask || !WorkItemTypes.IsCard(type))
            && await db.Sprints.AsNoTracking().Where(s => s.Id == sprintId && s.State != SprintState.Completed).Select(s => s.Name)
                .SingleOrDefaultAsync(ct) is { } sprintName)
        {
            lines.Add($"It leaves {sprintName}.");
        }

        if (item.Estimate is not null && !WorkItemTypes.HasEstimate(type, plan.Destination.Template))
        {
            lines.Add("Its story points are cleared.");
        }

        if (plan.IsOtherSpace)
        {
            var ids = plan.Subtasks.Select(s => s.Id).Prepend(item.Id).ToList();
            var links = await db.Dependencies.CountAsync(d => ids.Contains(d.PredecessorId) || ids.Contains(d.SuccessorId), ct);
            if (links > 0)
            {
                lines.Add($"Its {Count(links, "dependency", "dependencies")} {(links == 1 ? "is" : "are")} removed: dependencies link work of one space.");
            }

            var unassigned = 0;
            foreach (var assigneeId in plan.Subtasks.Prepend(item).Select(w => w.AssigneeId).OfType<Guid>())
            {
                unassigned += await team.CanBeAssignedAsync(plan.Destination.ProjectId, assigneeId, ct) ? 0 : 1;
            }

            if (unassigned > 0)
            {
                lines.Add($"{Count(unassigned, "assignee")} can't work in {plan.ToName}, so {(unassigned == 1 ? "that item becomes" : "those items become")} unassigned.");
            }
        }

        return lines;
    }

    /// <summary>A column of the destination with the same name, else the first of the same type, else the first "to do".</summary>
    private static StatusInfo MapStatus(StatusInfo current, IReadOnlyList<StatusInfo> statuses) =>
        statuses.FirstOrDefault(s => string.Equals(s.Name, current.Name, StringComparison.OrdinalIgnoreCase))
        ?? statuses.FirstOrDefault(s => s.Category == current.Category)
        ?? statuses.First(s => s.Category == StatusCategory.ToDo);

    private static string Count(int count, string noun, string? plural = null) =>
        $"{count} {(count == 1 ? noun : plural ?? noun + "s")}";

    private static ContainerRef RefOf(WorkItem container) =>
        new(container.Id, container.ProjectId, container.Key, container.Title, container.Type);

    private static StatusRef ToRef(StatusInfo status) => new(status.Id, status.Name, status.Category);
}
