using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The Kanban board (FR-016 to FR-022, research R14, R19).</summary>
internal sealed class BoardService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IWorkItemNumberAllocator numbers,
    CardRanker ranker,
    RankRebalancer rebalancer,
    AssigneeReads assignees,
    SprintReads sprints,
    StageLocks locks,
    StatusNotices notices,
    WatchList watchers,
    ItemEdits edits,
    INotificationCenter notifications,
    SearchIndexer indexer,
    TimeProvider time) : IBoardService
{
    /// <summary>"Done" columns show tasks completed within this window unless all are requested (FR-021).</summary>
    public static readonly TimeSpan DoneWindow = TimeSpan.FromDays(14);

    public Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct) =>
        GetAsync(projectKey, new BoardRead(showAllDone), ct);

    public async Task<Result<BoardView>> GetAsync(string projectKey, BoardRead read, CancellationToken ct)
    {
        var showAllDone = read.ShowAllDone;
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var info = await workflow.GetBoardInfoAsync(allowed.Value!.ProjectId, ct);
        var projectId = info.ProjectId;
        var doneIds = info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var cutoff = time.GetUtcNow() - DoneWindow;

        // A Scrum board shows the active sprint, all of its done cards included, and nothing without one (Phase 3 FR-027).
        var scrum = info.Template == ProjectTemplate.Scrum;
        var sprint = scrum ? await sprints.ActiveInfoAsync(projectId, ct) : null;
        var sprintId = sprint?.Id ?? 0;
        showAllDone |= scrum;
        var epics = read.ShowEpics && info.Template == ProjectTemplate.Kanban;
        var shown = scrum ? Cards(projectId).Where(w => w.SprintId == sprintId) : Cards(projectId, epics);
        var query = showAllDone ? shown : shown.Where(w => !doneIds.Contains(w.StatusId) || w.ResolvedAt >= cutoff);
        var rows = await query
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => new
            {
                w.Id,
                w.Key,
                w.Title,
                w.Priority,
                w.StatusId,
                w.RowVersion,
                w.AssigneeId,
                w.DueDate,
                w.Type,
                w.Estimate,
                w.ContainerId,
                w.Rank,
                w.CoverColor,
                w.CoverAttachmentId,
            })
            .ToListAsync(ct);

        // Sub-tasks are cards too, in their status's column under their parent: those of the cards the board can show (on a
        // Scrum board, the active sprint's), done ones within the window like the others (Jira parity pass FR-027a; R18).
        var subtaskQuery = SubtasksOf(shown);
        if (!showAllDone)
        {
            subtaskQuery = subtaskQuery.Where(s => !doneIds.Contains(s.Item.StatusId) || s.Item.ResolvedAt >= cutoff);
        }

        var subtaskRows = await subtaskQuery
            .Select(s => new
            {
                s.Item.Id,
                s.Item.Key,
                s.Item.Title,
                s.Item.Priority,
                s.Item.StatusId,
                s.Item.RowVersion,
                s.Item.AssigneeId,
                s.Item.DueDate,
                s.Item.Rank,
                s.ParentId,
                s.ParentKey,
                s.ParentRank,
                s.ParentContainerId,
                s.Item.CoverColor,
                s.Item.CoverAttachmentId,
            })
            .ToListAsync(ct);

        // The sub-task counts of every card in one read of the project's sub-tasks, rather than two lookups per card: at
        // 300 users those lookups were most of the database's work (SC-002).
        var subtasks = await db.WorkItems.AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .Select(g => new { ParentId = g.Key, Total = g.Count(), Done = g.Count(c => doneIds.Contains(c.StatusId)) })
            .ToDictionaryAsync(g => g.ParentId, ct);
        var people = await assignees.DescribeAsync(await assignees.TeamAsync(projectId, ct),
            rows.Select(r => r.AssigneeId).Concat(subtaskRows.Select(s => s.AssigneeId)).OfType<Guid>(), ct);
        // Labels and epics or stages for the cards and the quick filters (Phase 4 FR-045, FR-047).
        var labels = await LabelReads.ForItemsAsync(db, [.. rows.Select(r => r.Id), .. subtaskRows.Select(s => s.Id)], ct);
        var containers = await ContainerReads.ForIdsAsync(db,
            rows.Select(r => r.ContainerId).Concat(subtaskRows.Select(s => s.ParentContainerId)).OfType<long>(), ct);
        ContainerView? ContainerOf(long? containerId) => containerId is { } id ? containers.GetValueOrDefault(id) : null;
        AssigneeRef? PersonOf(Guid? assigneeId) => assigneeId is { } id ? people[id] : null;

        // Covers chosen with Jira's "Select cover": an image shows only while its file can be served.
        var coverIds = rows.Select(r => r.CoverAttachmentId).Concat(subtaskRows.Select(s => s.CoverAttachmentId)).OfType<long>().ToList();
        var covers = coverIds.Count == 0 ? [] : await db.Attachments.AsNoTracking()
            .Where(a => coverIds.Contains(a.Id) && a.CheckStatus == AttachmentCheckStatus.Passed && a.RemovedAt == null && a.PurgedAt == null)
            .Select(a => a.Id)
            .ToHashSetAsync(ct);
        long? CoverImage(long? attachmentId) => attachmentId is { } id && covers.Contains(id) ? id : null;

        // A card's place: its own rank; a sub-task's, its parent's and then its own (research R18).
        var cards = rows.Select(r =>
            {
                var counts = subtasks.GetValueOrDefault(r.Id);
                return (Order: (r.Rank, r.Id, Sub: ""), Card: new CardView(r.Key, r.Title, r.Priority, r.StatusId, counts?.Done ?? 0,
                    counts?.Total ?? 0, r.RowVersion, PersonOf(r.AssigneeId), r.DueDate, r.Type, r.Estimate, labels.GetValueOrDefault(r.Id),
                    ContainerOf(r.ContainerId))
                { CoverColor = r.CoverColor, CoverImageId = CoverImage(r.CoverAttachmentId) });
            })
            .Concat(subtaskRows.Select(s => (Order: (s.ParentRank, s.ParentId, Sub: s.Rank), Card: new CardView(s.Key, s.Title,
                s.Priority, s.StatusId, 0, 0, s.RowVersion, PersonOf(s.AssigneeId), s.DueDate, WorkItemType.Subtask, null,
                labels.GetValueOrDefault(s.Id), ContainerOf(s.ParentContainerId), s.ParentKey)
            { CoverColor = s.CoverColor, CoverImageId = CoverImage(s.CoverAttachmentId) })))
            .OrderBy(c => c.Order.Item1, StringComparer.Ordinal).ThenBy(c => c.Order.Item2).ThenBy(c => c.Order.Sub, StringComparer.Ordinal)
            .Select(c => c.Card)
            .ToList();

        var hiddenDone = showAllDone
            ? 0
            : await (scrum ? Cards(projectId) : Cards(projectId, epics)).CountAsync(w => doneIds.Contains(w.StatusId) && (w.ResolvedAt == null || w.ResolvedAt < cutoff), ct)
                + await SubtasksOf(shown).CountAsync(s => doneIds.Contains(s.Item.StatusId) && (s.Item.ResolvedAt == null || s.Item.ResolvedAt < cutoff), ct);

        var columns = info.Statuses.Select(s =>
        {
            var columnCards = cards.Where(c => c.ColumnId == s.Id).ToList();
            return new ColumnView(s.Id, s.Name, s.Category, s.WipLimit, columnCards.Count,
                s.WipLimit is { } limit && columnCards.Count > limit, columnCards);
        }).ToList();
        return new BoardView(info.Key, info.Name, info.BoardVersion, allowed.Value.CanManage, showAllDone, hiddenDone, columns,
            allowed.Value.UserId, CanRestoreDeleted: allowed.Value.IsAdministrator, CanContribute: allowed.Value.CanContribute,
            Template: info.Template, Sprint: sprint, CanManageSprints: scrum && allowed.Value.CanManage,
            WorkTypes: allowed.Value.WorkTypes);
    }

    public async Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title, WorkItemType type,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        // The board creates cards only: stories, tasks and bugs its template offers and the space has turned on (Phase 3
        // FR-010; Jira parity pass FR-017).
        if (!WorkItemTypes.IsCard(type))
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, $"The board creates stories, tasks and bugs; {WorkItemTypes.DisplayName(type)} is not one of them here.");
        }

        if (WorkItemTypes.RefusalFor(type, allowed.Value!.Template, allowed.Value.WorkTypes) is { } refusal)
        {
            return AppError.Rule(ErrorCodes.TypeNotAllowed, refusal);
        }

        if (WorkItem.ValidateTitle(title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var projectId = allowed.Value.ProjectId;
        var column = (await workflow.StatusesAsync(projectId, ct)).FirstOrDefault(s => s.Id == columnId);
        if (column is null)
        {
            return AppError.NotFound("column");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var number = await numbers.NextAsync(projectId, ct);
        var rank = await ranker.ForNewCardAsync(projectId, allowed.Value.Template, columnId, ct);
        var context = ChangeContext.New(allowed.Value.UserId, time.GetUtcNow());
        var item = WorkItem.Create(projectId, allowed.Value.Key, number, type, title, ToRef(column), rank, allowed.Value.Template,
            context, allowed.Value.WorkTypes).Value!;
        db.WorkItems.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.NotFound("column"); // the column was deleted a moment ago; nothing was created
        }

        await watchers.AddAsync(item.Id, [allowed.Value.UserId], ct); // the creator watches (Phase 4 FR-011)
        await indexer.IndexAsync(item, ct);
        await db.SaveChangesAsync(ct);

        // A card created on a Scrum board joins the active sprint (Phase 3 FR-027, research R12).
        if (allowed.Value.Template == ProjectTemplate.Scrum && await sprints.ActiveSprintAsync(projectId, ct) is { } active)
        {
            item.JoinSprint(null, active.Ref, context);
            sprints.Record(active, item, SprintScopeKind.Added, context);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new CardView(item.Key, item.Title, item.Priority, item.StatusId, 0, 0, item.RowVersion, null, null, item.Type, null);
    }

    public async Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement,
        byte[] expectedVersion, LaneChange? lane, CancellationToken ct)
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

        var isSubtask = item.ParentId is not null;
        // Epics are cards only when a Kanban board shows them; stages and milestones never are.
        var epicCard = item.Type == WorkItemType.Epic && allowed.Value!.Template == ProjectTemplate.Kanban;
        if (!isSubtask && !WorkItemTypes.IsCard(item.Type) && !epicCard)
        {
            return AppError.Validation("Key", "Stages and milestones are not shown on the board; change their status in their details.");
        }

        if (isSubtask && lane is LaneChange.Container)
        {
            return AppError.Rule(ErrorCodes.SubtaskFollowsParent,
                $"A sub-task follows its parent's {(allowed.Value!.Template == ProjectTemplate.StageGate ? "stage" : "epic")}.");
        }

        var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
        var target = statuses.FirstOrDefault(s => s.Id == toColumnId);
        if (target is null)
        {
            return AppError.NotFound("column");
        }

        if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(item, ct);
        }

        var current = statuses.Single(s => s.Id == item.StatusId);
        if (lane is null && await locks.CheckMoveAsync(item, current, target, ct) is { } locked)
        {
            return locked; // work in a locked stage stays in "to do" (Phase 3 FR-032)
        }

        // The rank first: renumbering a full column saves, and must not save half of a lane change. A sub-task card keeps
        // its place under its parent (research R18).
        var (rank, note) = isSubtask ? (item.Rank, "") : await RankForAsync(item, allowed.Value!.Template, target.Id, placement, ct);
        int? oldPosition = null;
        int? newPosition = null;
        if (target.Id == current.Id && rank != item.Rank)
        {
            oldPosition = await ranker.PositionOfAsync(item, ct);
            newPosition = await ranker.CountBeforeAsync(item, rank, ct) + 1;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var context = ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow());
        var events = new List<NotificationEvent>();
        var changes = item.Changes.Count;

        // The lane, then the column, in one change: the column move is checked against the card's new epic or stage, and
        // a refusal leaves the card as it was (Jira parity pass FR-027; research R17).
        switch (lane)
        {
            case LaneChange.Container container when await edits.PlaceInAsync(item, container.ContainerKey, target.Category, context, ct) is { } misplaced:
                return misplaced;
            case LaneChange.Assignee assignee when await edits.AssignAsync(item, assignee.UserId, context, events, ct) is { } notAssignable:
                return notAssignable;
            case LaneChange.Priority priority when priority.Value != item.Priority:
                item.Prioritize(priority.Value, context);
                break;
        }

        if (lane is not null && await locks.CheckMoveAsync(item, current, target, ct) is { } lockedHere)
        {
            return lockedHere;
        }

        if (target.Id == current.Id && rank == item.Rank && item.Changes.Count == changes)
        {
            return await CardAsync(item, statuses, ct);
        }

        if (target.Id != current.Id || rank != item.Rank)
        {
            item.Move(ToRef(current), ToRef(target), rank, oldPosition, newPosition, target.Id == current.Id ? note : null, context);
        }

        if (target.Id != current.Id)
        {
            await notices.RaiseAsync([item], target.Name, allowed.Value.UserId, ct);
        }

        await notifications.RaiseAsync(events, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(item, ct);
        }
        catch (DbUpdateException)
        {
            // The target column was deleted after the board was read.
            return AppError.Conflict("The board's columns changed while the card was moving. The latest board is shown.");
        }

        var card = await CardAsync(item, statuses, ct);
        var warnings = target.Category == StatusCategory.Done && current.Category != StatusCategory.Done
            ? await OpenSubtaskWarningAsync(item, statuses, ct)
            : [];
        return Result<CardView>.Ok(card.Value!, warnings);
    }

    private async Task<(string Rank, string Note)> RankForAsync(WorkItem item, ProjectTemplate template, long columnId,
        CardPlacement placement, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (rank, note) = placement switch
            {
                CardPlacement.Keep => (item.Rank, ""),
                CardPlacement.Top => (await ranker.AtEdgeAsync(item.ProjectId, template, columnId, item.Id, item.Rank, atTop: true, ct), "moved to top"),
                CardPlacement.Before before when await ranker.BeforeCardAsync(item, template, columnId, before.WorkItemKey, ct) is { } between =>
                    (between, $"moved above {before.WorkItemKey}"),
                _ => (await ranker.AtEdgeAsync(item.ProjectId, template, columnId, item.Id, item.Rank, atTop: false, ct), "moved to bottom"),
            };
            if (rank.Length <= Rank.MaxLength || attempt > 0)
            {
                return (rank, note);
            }

            // The gap is exhausted: renumber the column, or the whole Scrum project (order unchanged), and try again.
            await rebalancer.RebalanceAsync(item.ProjectId, template, columnId, ct);
        }
    }

    /// <summary>The project's live cards: top-level stories, tasks and bugs, as <c>IX_WorkItems_Board</c> holds them
    /// (Phase 3 FR-013).</summary>
    private IQueryable<WorkItem> Cards(long projectId, bool epics = false) =>
        db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId && w.ParentId == null
            && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug || (epics && w.Type == WorkItemType.Epic)));

    private async Task<AppError> ConflictAsync(WorkItem item, CancellationToken ct)
    {
        await db.Entry(item).ReloadAsync(ct);
        var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
        return AppError.Conflict($"{item.Key} was changed by someone else. The board now shows its current position.",
            (await CardAsync(item, statuses, ct)).Value);
    }

    private async Task<Result<CardView>> CardAsync(WorkItem item, IReadOnlyList<StatusInfo> statuses, CancellationToken ct)
    {
        var doneIds = statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var subtasks = await db.WorkItems.AsNoTracking()
            .Where(c => c.ParentId == item.Id)
            .Select(c => c.StatusId)
            .ToListAsync(ct);
        var assignee = item.AssigneeId is { } assigneeId
            ? (await assignees.DescribeAsync(await assignees.TeamAsync(item.ProjectId, ct), [assigneeId], ct))[assigneeId]
            : null;

        // A sub-task card shows its parent and takes its parent's epic or stage (research R18).
        var (parentKey, containerId) = (default(string), item.ContainerId);
        if (item.ParentId is { } parentId)
        {
            var parent = await db.WorkItems.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == parentId)
                .Select(p => new { p.Key, p.ContainerId }).SingleAsync(ct);
            (parentKey, containerId) = (parent.Key, parent.ContainerId);
        }

        var container = containerId is { } id ? (await ContainerReads.ForIdsAsync(db, [id], ct)).GetValueOrDefault(id) : null;
        return new CardView(item.Key, item.Title, item.Priority, item.StatusId, subtasks.Count(doneIds.Contains),
            subtasks.Count, item.RowVersion, assignee, item.DueDate, item.Type, item.Estimate,
            await LabelReads.ForItemAsync(db, item.Id, ct), container, parentKey);
    }

    /// <summary>The live sub-tasks of the given cards, with what a sub-task card takes from its parent.</summary>
    private IQueryable<SubtaskRow> SubtasksOf(IQueryable<WorkItem> cards) =>
        from s in db.WorkItems.AsNoTracking()
        join p in cards on s.ParentId equals (long?)p.Id
        select new SubtaskRow { Item = s, ParentId = p.Id, ParentKey = p.Key, ParentRank = p.Rank, ParentContainerId = p.ContainerId };

    private sealed class SubtaskRow
    {
        public required WorkItem Item { get; init; }

        public long ParentId { get; init; }

        public required string ParentKey { get; init; }

        public required string ParentRank { get; init; }

        public long? ParentContainerId { get; init; }
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
}
