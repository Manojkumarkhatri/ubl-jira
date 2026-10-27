using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The details drawer (FR-023 to FR-033), and creating work items of every type (Phase 3 FR-010).</summary>
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
    SprintReads sprintReads,
    StageLocks locks,
    StageGateRules gates,
    DependencyReads dependencies,
    IPlanLock planLock,
    INotificationCenter notifications,
    WatchList watchers,
    ItemEdits edits,
    StatusNotices notices,
    TextChecks texts,
    AttachmentService attachments,
    SearchIndexer indexer,
    TimeProvider time) : IWorkItemService
{
    public async Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct)
            ?? await MovedItemAsync(workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        await RecordViewAsync(item.Id, allowed.Value!.UserId, ct);
        return await DetailsAsync(item, allowed.Value, ct);
    }

    /// <summary>An item moved to another space keeps answering to its old key, as in Jira.</summary>
    private async Task<WorkItem?> MovedItemAsync(string oldKey, CancellationToken ct)
    {
        var id = await db.WorkItemChanges.AsNoTracking()
            .Where(c => c.Field == WorkItemField.Moved && c.OldValue == oldKey)
            .OrderByDescending(c => c.Id)
            .Select(c => (long?)c.WorkItemId)
            .FirstOrDefaultAsync(ct);
        return id is { } movedId ? await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Id == movedId, ct) : null;
    }

    /// <summary>Opening the drawer is a recent view for the search box; each person keeps their latest 50 (Phase 4 FR-033).</summary>
    private async Task RecordViewAsync(long workItemId, Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (await db.RecentViews.Where(r => r.UserId == userId && r.WorkItemId == workItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ViewedAt, now), ct) > 0)
        {
            return;
        }

        var view = RecentView.Of(userId, workItemId, now);
        db.RecentViews.Add(view);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(view).State = EntityState.Detached; // opened in two windows at once: one view is enough
            return;
        }

        var oldestKept = await db.RecentViews.Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ViewedAt)
            .Skip(RecentView.Kept - 1)
            .Select(r => (DateTimeOffset?)r.ViewedAt)
            .FirstOrDefaultAsync(ct);
        if (oldestKept is { } kept)
        {
            await db.RecentViews.Where(r => r.UserId == userId && r.ViewedAt < kept).ExecuteDeleteAsync(ct);
        }
    }

    public async Task<Result<WorkItemDetails>> CreateAsync(string projectKey, NewWorkItem newItem, CancellationToken ct)
    {
        // Stages are created by Project Admins and administrators, like the rest of the stage order (Phase 3 FR-039).
        var allowed = await access.RequireAsync(projectKey,
            newItem.Type == WorkItemType.Stage ? ProjectRight.Manage : ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var isSubtask = newItem.Type == WorkItemType.Subtask;
        if (isSubtask && newItem.ParentKey is null)
        {
            return AppError.Rule(ErrorCodes.ParentRequired, "Choose the item this sub-task belongs to.");
        }

        if (!isSubtask && WorkItemTypes.RefusalFor(newItem.Type, caller.Template, caller.WorkTypes) is { } refusal)
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, refusal);
        }

        if (WorkItem.ValidateTitle(newItem.Title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        // A sub-task's parent is a live story, task or bug of this space (Jira parity pass FR-005).
        WorkItem? parent = null;
        if (newItem.ParentKey is { } parentKey)
        {
            parent = isSubtask ? await db.WorkItems.SingleOrDefaultAsync(w => w.Key == parentKey && w.ProjectId == caller.ProjectId, ct) : null;
            if (parent is null || parent.ParentId is not null || !WorkItemTypes.IsCard(parent.Type))
            {
                return AppError.Rule(ErrorCodes.InvalidParent, "The parent item no longer exists.");
            }
        }

        ContainerRef? container = null;
        if (newItem.ContainerKey is { } containerKey)
        {
            container = await edits.ContainerAsync(caller.ProjectId, containerKey, ct);
            if (container is null || WorkItemTypes.LevelOf(newItem.Type) != WorkItemLevel.Standard)
            {
                return ItemEdits.InvalidContainer();
            }
        }

        var statuses = await workflow.StatusesAsync(caller.ProjectId, ct);
        var firstToDo = statuses.First(s => s.Category == StatusCategory.ToDo);
        var status = newItem.StatusId is { } statusId ? statuses.FirstOrDefault(s => s.Id == statusId) : firstToDo;
        if (status is null)
        {
            return AppError.NotFound("column");
        }

        if (newItem.AssigneeId is { } assigneeId && !await team.CanBeAssignedAsync(caller.ProjectId, assigneeId, ct))
        {
            return AppError.Rule(ErrorCodes.NotAssignable, ItemEdits.NotAssignableMessage);
        }

        Sprint? sprint = null;
        if (newItem.SprintId is { } sprintId)
        {
            sprint = await db.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == caller.ProjectId, ct);
            if (sprint is null)
            {
                return AppError.NotFound("sprint");
            }
        }

        FormattedText? description = null;
        if (newItem.Description is { } document)
        {
            // A new item has no attachments yet, so its description shows no images.
            var parsed = await texts.ParseAsync(document, "Description", caller.ProjectId, 0, ct);
            if (!parsed.IsSuccess)
            {
                return parsed.Error!.ToAppError();
            }

            description = parsed.Value!;
        }

        var labels = (newItem.Labels ?? []).DistinctBy(Label.Normalize).ToList();
        foreach (var label in labels)
        {
            if (Label.Check(label) is { } invalidLabel)
            {
                return invalidLabel.ToAppError();
            }
        }

        if (labels.Count > 0 && Label.CheckRoom(labels.Count - 1) is { } tooMany)
        {
            return tooMany.ToAppError();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (newItem.Type == WorkItemType.Stage)
        {
            await planLock.AcquireAsync(caller.ProjectId, ct); // the stage order (Phase 3 research R9)
        }

        var number = await numbers.NextAsync(caller.ProjectId, ct);
        var rank = newItem.Type switch
        {
            WorkItemType.Subtask => await ranker.AfterLastSiblingAsync(parent!.Id, ct),
            _ when WorkItemTypes.IsCard(newItem.Type) => await ranker.ForNewCardAsync(caller.ProjectId, caller.Template, status.Id, ct),
            WorkItemType.Stage => Rank.After(await LastStageRankAsync(caller.ProjectId, ct)),
            _ => Rank.First(), // epics and milestones are in no card order (Phase 3 research R4)
        };

        // Every value is one change in the item's history (Jira parity pass FR-006).
        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        var created = parent is not null
            ? parent.AddSubtask(number, newItem.Title, ToRef(status), rank, context)
            : WorkItem.Create(caller.ProjectId, caller.Key, number, newItem.Type, newItem.Title, ToRef(status), rank, caller.Template,
                context, caller.WorkTypes);
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var item = created.Value!;
        if (await ApplyNewFieldsAsync(item, newItem, caller, status, firstToDo, container, sprint, description, context, ct) is { } refused)
        {
            return refused;
        }

        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        foreach (var label in labels)
        {
            var shared = await LabelService.SharedAsync(db, label, ct);
            db.WorkItemLabels.Add(WorkItemLabel.Add(item.Id, shared.Id, caller.UserId, context.At));
            item.RecordActivity(WorkItemField.LabelAdded, null, shared.Name, context);
        }

        if (sprint is { State: SprintState.Active })
        {
            sprintReads.Record(sprint, item, SprintScopeKind.Added, context);
        }

        // The creator watches, and so does an assignee, who is told; mentioned people are told (Phase 4 FR-009, FR-011, FR-015).
        await watchers.AddAsync(item.Id, new[] { caller.UserId, newItem.AssigneeId ?? caller.UserId }.Distinct().ToList(), ct);
        await notifications.RaiseAsync(
        [
            new NotificationEvent(NotificationKind.Mentioned, caller.ProjectId, item.Id, caller.UserId, description?.MentionIds ?? []),
            new NotificationEvent(NotificationKind.Assigned, caller.ProjectId, item.Id, caller.UserId,
                newItem.AssigneeId is { } assigned ? [assigned] : []),
        ], ct);
        await indexer.IndexAsync(item, ct);
        if (item.Type == WorkItemType.Stage)
        {
            gates.AddGateFor(item); // every stage ends in a gate (Phase 3 FR-031)
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailsAsync(item, caller, ct);
    }

    /// <summary>The Create form's values on a new item, with the drawer's rules (Jira parity pass FR-006).</summary>
    private async Task<AppError?> ApplyNewFieldsAsync(WorkItem item, NewWorkItem newItem, ProjectAccessInfo caller, StatusInfo status,
        StatusInfo firstToDo, ContainerRef? container, Sprint? sprint, FormattedText? description, ChangeContext context, CancellationToken ct)
    {
        // Started work cannot be created in a locked stage, nor a sub-task of work in one (Phase 3 FR-032, FR-033).
        if (status.Category != StatusCategory.ToDo || item.Type == WorkItemType.Stage)
        {
            var locked = container is not null
                ? await locks.CheckPlaceInAsync(item, status.Category, container.Id, ct)
                : await locks.CheckMoveAsync(item, firstToDo, status, ct);
            if (locked is not null)
            {
                return locked;
            }
        }

        if (container is not null && item.PlaceIn(null, container, context) is { } misplaced)
        {
            return misplaced.ToAppError();
        }

        if (description is not null && item.Describe(description, context) is { } invalidText)
        {
            return invalidText.ToAppError();
        }

        if (newItem.Priority is { } priority && priority != item.Priority)
        {
            item.Prioritize(priority, context);
        }

        if (newItem.AssigneeId is { } assigneeId)
        {
            var names = await users.GetAsync([assigneeId], ct);
            item.Assign(null, ItemEdits.Person(assigneeId, names), context);
        }

        if ((newItem.StartDate is not null || newItem.DueDate is not null) && item.Schedule(newItem.StartDate, newItem.DueDate, context) is { } invalidDates)
        {
            return invalidDates.ToAppError();
        }

        if (newItem.Estimate is not null && item.SetEstimate(newItem.Estimate, caller.Template, context) is { } invalidEstimate)
        {
            return invalidEstimate.ToAppError();
        }

        if (sprint is not null && item.JoinSprint(null, sprint.Ref, context) is { } notInSprint)
        {
            return notInSprint.ToAppError();
        }

        return null;
    }

    public async Task<Result<IReadOnlyList<ContainerOption>>> ListContainersAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var containers = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == allowed.Value!.ProjectId && (w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage))
            .Select(w => new { w.Key, w.Title, w.Type, w.Rank, w.Number })
            .ToListAsync(ct);
        // Stages in stage order (Phase 3 research R13), epics by key.
        return containers
            .OrderBy(c => c.Type == WorkItemType.Stage ? c.Rank : "", StringComparer.Ordinal)
            .ThenBy(c => c.Number)
            .Select(c => new ContainerOption(c.Key, c.Title, c.Type, IsLocked: false))
            .ToList();
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

        var caller = allowed.Value!;
        if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(item, caller, ct);
        }

        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        IReadOnlyList<string> warnings = [];
        var events = new List<NotificationEvent>();
        switch (edit)
        {
            case WorkItemEdit.Title title when item.Rename(title.Value, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Description description:
                FormattedText? text = null;
                if (description.Value is { } document)
                {
                    var parsed = await texts.ParseAsync(document, "Description", item.ProjectId, item.Id, ct);
                    if (!parsed.IsSuccess)
                    {
                        return parsed.Error!.ToAppError();
                    }

                    text = parsed.Value!;
                }

                // Only people mentioned for the first time in this description are told (FR-009).
                var mentionedBefore = TextChecks.MentionsIn(item.DescriptionFormat, item.Description);
                if (item.Describe(text, context) is { } invalidText)
                {
                    return invalidText.ToAppError();
                }

                var newMentions = (text?.MentionIds ?? []).Except(mentionedBefore).ToList();
                events.Add(new NotificationEvent(NotificationKind.Mentioned, item.ProjectId, item.Id, caller.UserId, newMentions));
                break;
            case WorkItemEdit.Priority priority:
                item.Prioritize(priority.Value, context);
                break;
            case WorkItemEdit.Assignee assignee when await edits.AssignAsync(item, assignee.UserId, context, events, ct) is { } notAssignable:
                return notAssignable;
            case WorkItemEdit.Dates dates when item.Schedule(dates.Start, dates.Due, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Type type when item.ChangeType(type.Value, caller.Template, context) is { } refused:
                return refused.ToAppError();
            case WorkItemEdit.Estimate estimate when item.SetEstimate(estimate.Value, caller.Template, context) is { } invalid:
                return invalid.ToAppError();
            case WorkItemEdit.Color color when item.SetColor(color.Value, context) is { } refused:
                return refused.ToAppError();
            case WorkItemEdit.Container container:
                // Started work cannot join a locked stage (Phase 3 FR-033).
                var category = (await workflow.StatusesAsync(item.ProjectId, ct)).Single(s => s.Id == item.StatusId).Category;
                if (await edits.PlaceInAsync(item, container.ContainerKey, category, context, ct) is { } misplaced)
                {
                    return misplaced;
                }

                break;
            case WorkItemEdit.Status status:
                var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
                var column = statuses.FirstOrDefault(s => s.Id == status.ColumnId);
                if (column is null)
                {
                    return AppError.NotFound("column");
                }

                var current = statuses.Single(s => s.Id == item.StatusId);
                if (column.Id != current.Id && await locks.CheckMoveAsync(item, current, column, ct) is { } locked)
                {
                    return locked; // work in a locked stage stays in "to do" (Phase 3 FR-032, FR-034)
                }

                if (column.Id != current.Id)
                {
                    // A card goes to the bottom of its new column; sub-tasks, epics, stages and milestones keep their rank,
                    // and so do Scrum cards, whose rank is the backlog's and the sprint's order (Phase 3 research R8).
                    var rank = item.ParentId is null && WorkItemTypes.IsCard(item.Type) && caller.Template != ProjectTemplate.Scrum
                        ? await ranker.AtEdgeAsync(item.ProjectId, caller.Template, column.Id, item.Id, item.Rank, atTop: false, ct)
                        : item.Rank;
                    item.Move(ToRef(current), ToRef(column), rank, null, null, null, context);
                    await notices.RaiseAsync([item], column.Name, caller.UserId, ct);
                    if (column.Category == StatusCategory.Done && current.Category != StatusCategory.Done)
                    {
                        warnings = [.. await OpenSubtaskWarningAsync(item, statuses, ct), .. await OpenItemsWarningAsync(item, ct)];
                    }
                }

                break;
        }

        if (edit is WorkItemEdit.Title or WorkItemEdit.Description)
        {
            await indexer.IndexAsync(item, ct); // its search words change with it (Phase 4 research R15)
        }

        await notifications.RaiseAsync(events, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone else saved first, or the chosen column was deleted a moment ago.
            return await ConflictAsync(item, caller, ct);
        }

        return Result<WorkItemDetails>.Ok(await DetailsAsync(item, caller, ct), warnings);
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

        if (WorkItem.ValidateTitle(title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var firstToDo = await workflow.FirstToDoStatusAsync(parent.ProjectId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var number = await numbers.NextAsync(parent.ProjectId, ct);
        var rank = await ranker.AfterLastSiblingAsync(parent.Id, ct);
        // Sub-tasks cannot have sub-tasks, and milestones, epics and stages have none (Phase 3 FR-008, FR-012).
        var created = parent.AddSubtask(number, title, ToRef(firstToDo), rank,
            ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.WorkItems.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        await watchers.AddAsync(created.Value!.Id, [allowed.Value.UserId], ct); // the creator watches (Phase 4 FR-011)
        await indexer.IndexAsync(created.Value, ct);
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
            if (await locks.CheckMoveAsync(item, current, done, ct) is { } locked)
            {
                return locked; // work in a locked stage stays in "to do" (Phase 3 FR-032)
            }

            item.Move(ToRef(current), ToRef(done), item.Rank, null, null, null,
                ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
            await notices.RaiseAsync([item], done.Name, allowed.Value.UserId, ct);
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

        if (await RefuseDeleteAsync(item, allowed.Value!, ct) is { } refused)
        {
            return refused;
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

        if (await RefuseDeleteAsync(item, allowed.Value!, ct) is { } refused)
        {
            return refused;
        }

        // A stage is part of the stage order: an approved one keeps its place, and deleting another cancels its gate's
        // pending request, under the planning lock (Phase 3 FR-039, research R9).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var context = ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow());
        if (item.Type == WorkItemType.Stage)
        {
            await planLock.AcquireAsync(item.ProjectId, ct);
            if (await gates.CheckDeleteAsync(item, ct) is { } approved)
            {
                return approved;
            }

            await gates.ResetAsync(item, context, ct);
        }

        var subtasks = await db.WorkItems.Where(w => w.ParentId == item.Id).ToListAsync(ct);
        item.Delete(subtasks, context);

        // Its dependencies, and its sub-tasks', go with it; the items they linked record that (Phase 3 FR-052).
        await dependencies.RemoveLinksAsync([item, .. subtasks], context, ct);

        // A deleted epic or stage lets its items go: they stay in the project, in no container (Phase 3 FR-019).
        if (WorkItemTypes.LevelOf(item.Type) == WorkItemLevel.Container)
        {
            var self = new ContainerRef(item.Id, item.ProjectId, item.Key, item.Title, item.Type);
            foreach (var contained in await db.WorkItems.Where(w => w.ContainerId == item.Id).ToListAsync(ct))
            {
                contained.PlaceIn(self, null, context);
            }
        }

        // A card leaves its planned or active sprint in the same change set, so a restored card returns to the backlog;
        // leaving an active sprint counts as removed in its report (Phase 3 research R10, R11).
        if (item.SprintId is { } sprintId && await db.Sprints.SingleAsync(s => s.Id == sprintId, ct) is { State: not SprintState.Completed } sprint)
        {
            item.JoinSprint(sprint.Ref, null, context);
            if (sprint.State == SprintState.Active)
            {
                sprintReads.Record(sprint, item, SprintScopeKind.Removed, context);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
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
                w.Type,
                w.DeletedAt,
                w.DeletedById,
                Subtasks = all.Count(c => c.ParentId == w.Id && c.IsDeleted && c.DeletedAt == w.DeletedAt),
            })
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Where(r => r.DeletedById != null).Select(r => r.DeletedById!.Value).Distinct().ToList(), ct);
        var items = rows.ConvertAll(r => new DeletedItemView(r.Key, r.Title, r.DeletedAt!.Value,
            r.DeletedById is { } by ? WorkItemReads.NameOf(names, by) : "Unknown user", r.Subtasks)
        { Type = r.Type });
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

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var context = ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow());
        var subtasks = await db.WorkItems.IgnoreQueryFilters().Where(w => w.ParentId == item.Id).ToListAsync(ct);
        item.Restore(subtasks, context);

        // In a Scrum project a restored card returns at the bottom of the project's order (Phase 3 research R8).
        if (allowed.Value.Template == ProjectTemplate.Scrum && item.ParentId is null && WorkItemTypes.IsCard(item.Type))
        {
            item.Rerank(await ranker.AfterAllInProjectAsync(item.ProjectId, item.Id, ct));
        }

        // A restored stage comes back last, with its gate not requested, so it never locks work already started
        // (data-model.md "Gate rules").
        if (item.Type == WorkItemType.Stage)
        {
            await planLock.AcquireAsync(item.ProjectId, ct);
            item.Rerank(Rank.After(await LastStageRankAsync(item.ProjectId, ct)));
            await gates.ResetAsync(item, context, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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
        var container = item.ContainerId is { } containerId
            ? await db.WorkItems.AsNoTracking().Where(w => w.Id == containerId)
                .Select(w => new ContainerView(w.Key, w.Title, w.Type, w.Color)).SingleOrDefaultAsync(ct)
            : null;
        var creator = await users.GetAsync([item.CreatedById], ct);
        var members = await assignees.TeamAsync(item.ProjectId, ct);
        var assignee = item.AssigneeId is { } assigneeId
            ? (await assignees.DescribeAsync(members, [assigneeId], ct))[assigneeId]
            : null;
        var asContainer = WorkItemTypes.LevelOf(item.Type) == WorkItemLevel.Container
            ? await reads.ContainedAsync(item.Id, item.ResolvedAt is not null, info.Statuses, members, PageRequest.First, ct)
            : null;
        var sprint = item.SprintId is { } sprintId
            ? await db.Sprints.AsNoTracking().Where(s => s.Id == sprintId)
                .Select(s => new SprintView(s.Number, s.Name, s.State)).SingleOrDefaultAsync(ct)
            : null;
        if (sprint is { State: SprintState.Completed } && item.ResolvedAt is null)
        {
            sprint = null; // reopened after its sprint ended: back in the backlog (Phase 3 research R10)
        }

        // Stage-gate work: the gate a locked item waits for, and a stage's own gate (Phase 3 FR-031, FR-032).
        var lockedUntil = info.Template == ProjectTemplate.StageGate ? (await locks.BlockingGateAsync(item, ct))?.Title : null;
        var gate = item.Type == WorkItemType.Stage ? await gates.SummaryAsync(item.Id, ct) : null;
        var details = new WorkItemDetails(
            item.Key,
            info.Key,
            info.Name,
            item.Type,
            item.Title,
            (await reads.WithCurrentNamesAsync([RichText.Of(item.DescriptionFormat, item.Description, item.DescriptionText)], ct))[0],
            item.Priority,
            WorkItemReads.ToOption(info.Statuses, item.StatusId),
            info.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(),
            parent,
            WorkItemReads.NameOf(creator, item.CreatedById),
            item.CreatedAt,
            item.UpdatedAt,
            item.ResolvedAt,
            (await RefuseDeleteAsync(item, allowed, ct)) is null,
            item.RowVersion,
            await reads.SubtasksAsync(item.Id, info.Statuses, members, PageRequest.First, ct),
            await reads.CommentsAsync(item.Id, allowed.UserId, PageRequest.First, ct),
            await reads.HistoryAsync(item.Id, PageRequest.First, ct),
            assignee,
            item.StartDate,
            item.DueDate,
            AssigneeReads.Options(members, allowed.UserId, allowed.CanContribute),
            allowed.CanContribute,
            info.Template,
            WorkItemTypes.ChoicesFor(item.Type, info.Template),
            container,
            item.Estimate,
            asContainer,
            sprint,
            gate,
            lockedUntil,
            await dependencies.ListAsync(item, info.Statuses, ct),
            await watchers.ViewAsync(item.Id, allowed, ct),
            await attachments.ViewsAsync(item.Id, allowed, ct),
            await LabelReads.ForItemAsync(db, item.Id, ct));
        var (layout, fieldValues) = await FieldReads.ForItemAsync(db, users, item, info.Template, ct);
        var votes = await db.WorkItemVotes.AsNoTracking().Where(v => v.WorkItemId == item.Id).Select(v => v.UserId).ToListAsync(ct);
        var hasVoted = votes.Contains(allowed.UserId);
        return details with
        {
            Color = item.Color, // an epic's (Jira parity pass FR-025)
            OriginalEstimateMinutes = item.OriginalEstimateMinutes,
            RemainingEstimateMinutes = item.RemainingEstimateMinutes,
            TimeSpentMinutes = item.TimeSpentMinutes,
            Votes = votes.Count,
            HasVoted = hasVoted,
            VoteRefusal = hasVoted ? null : WorkItemActionService.VoteRefusal(item, allowed.UserId),
            CoverColor = item.CoverColor,
            CoverAttachmentId = item.CoverAttachmentId,
            CanManage = allowed.CanManage || allowed.IsAdministrator,
            Fields = layout,
            CustomFields = fieldValues,
        };
    }

    /// <summary>Who may delete an item: the ticks "Delete any work item" and "Delete work items they created" (owner,
    /// 2026-10-26); a stage also needs someone who manages the space (Phase 3 FR-019).</summary>
    private async Task<AppError?> RefuseDeleteAsync(WorkItem item, ProjectAccessInfo allowed, CancellationToken ct)
    {
        if (item.Type == WorkItemType.Stage)
        {
            if (!allowed.CanManage)
            {
                return AppError.Forbidden("Only someone who manages the space, or an Admin, can delete a stage.");
            }
        }

        return await access.CanDeleteWorkItemAsync(item.ProjectId, item.CreatedById, ct)
            ? null
            : AppError.Forbidden("You can't delete this work item. It needs the \"Delete any work item\" right, or \"Delete work items they created\" for your own. Ask an Admin.");
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

    /// <summary>An epic or stage completed while items in it are open is allowed, with a warning naming them (Phase 3
    /// FR-018).</summary>
    private async Task<IReadOnlyList<string>> OpenItemsWarningAsync(WorkItem container, CancellationToken ct)
    {
        if (WorkItemTypes.LevelOf(container.Type) != WorkItemLevel.Container)
        {
            return [];
        }

        var open = await db.WorkItems.AsNoTracking()
            .Where(w => w.ContainerId == container.Id && w.ResolvedAt == null)
            .OrderBy(w => w.Number)
            .Select(w => w.Key)
            .ToListAsync(ct);
        return open.Count == 0
            ? []
            : [$"{container.Key} is done but {open.Count} of its item{(open.Count == 1 ? " is" : "s are")} still open: {string.Join(", ", open)}."];
    }

    /// <summary>The largest stage rank, so a new stage goes last (Phase 3 research R13).</summary>
    private Task<string?> LastStageRankAsync(long projectId, CancellationToken ct) =>
        db.WorkItems.Where(w => w.ProjectId == projectId && w.Type == WorkItemType.Stage).MaxAsync(w => (string?)w.Rank, ct);

    private static StatusRef ToRef(StatusInfo status) => new(status.Id, status.Name, status.Category);
}
