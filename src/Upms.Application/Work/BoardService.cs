using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
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
    TimeProvider time) : IBoardService
{
    /// <summary>"Done" columns show tasks completed within this window unless all are requested (FR-021).</summary>
    public static readonly TimeSpan DoneWindow = TimeSpan.FromDays(14);

    public async Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var info = await workflow.GetBoardInfoAsync(allowed.Value!.ProjectId, ct);
        var projectId = info.ProjectId;
        var doneIds = info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var cutoff = time.GetUtcNow() - DoneWindow;

        var query = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId && w.ParentId == null);
        if (!showAllDone)
        {
            query = query.Where(w => !doneIds.Contains(w.StatusId) || w.ResolvedAt >= cutoff);
        }

        var cards = await query
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => new CardView(w.Key, w.Title, w.Priority, w.StatusId,
                db.WorkItems.Count(c => c.ParentId == w.Id && doneIds.Contains(c.StatusId)),
                db.WorkItems.Count(c => c.ParentId == w.Id),
                w.RowVersion))
            .ToListAsync(ct);

        var hiddenDone = showAllDone
            ? 0
            : await db.WorkItems.CountAsync(w => w.ProjectId == projectId && w.ParentId == null
                && doneIds.Contains(w.StatusId) && (w.ResolvedAt == null || w.ResolvedAt < cutoff), ct);

        var columns = info.Statuses.Select(s =>
        {
            var columnCards = cards.Where(c => c.ColumnId == s.Id).ToList();
            return new ColumnView(s.Id, s.Name, s.Category, s.WipLimit, columnCards.Count,
                s.WipLimit is { } limit && columnCards.Count > limit, columnCards);
        }).ToList();
        return new BoardView(info.Key, info.Name, info.BoardVersion, allowed.Value.CanManage, showAllDone, hiddenDone, columns,
            CanRestoreDeleted: allowed.Value.IsAdministrator, CanContribute: allowed.Value.CanContribute);
    }

    public async Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (WorkItem.ValidateTitle(title) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var projectId = allowed.Value!.ProjectId;
        var column = (await workflow.StatusesAsync(projectId, ct)).FirstOrDefault(s => s.Id == columnId);
        if (column is null)
        {
            return AppError.NotFound("column");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var number = await numbers.NextAsync(projectId, ct);
        var rank = await ranker.AtEdgeAsync(projectId, columnId, excludeId: null, atTop: false, ct);
        var item = WorkItem.CreateTask(projectId, allowed.Value.Key, number, title, ToRef(column), rank,
            ChangeContext.New(allowed.Value.UserId, time.GetUtcNow())).Value!;
        db.WorkItems.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.NotFound("column"); // the column was deleted a moment ago; nothing was created
        }

        await transaction.CommitAsync(ct);
        return new CardView(item.Key, item.Title, item.Priority, item.StatusId, 0, 0, item.RowVersion);
    }

    public async Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement,
        byte[] expectedVersion, CancellationToken ct)
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

        if (item.ParentId is not null)
        {
            return AppError.Validation("Key", "Sub-tasks are not shown on the board; change their status in the task.");
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
        var (rank, note) = await RankForAsync(item, target.Id, placement, ct);
        int? oldPosition = null;
        int? newPosition = null;
        if (target.Id == current.Id)
        {
            if (rank == item.Rank)
            {
                return await CardAsync(item, statuses, ct);
            }

            oldPosition = await ranker.PositionOfAsync(item, ct);
            newPosition = await ranker.CountBeforeAsync(item, rank, ct) + 1;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        item.Move(ToRef(current), ToRef(target), rank, oldPosition, newPosition,
            target.Id == current.Id ? note : null, ChangeContext.New(allowed.Value!.UserId, time.GetUtcNow()));
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

    private async Task<(string Rank, string Note)> RankForAsync(WorkItem item, long columnId, CardPlacement placement,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (rank, note) = placement switch
            {
                CardPlacement.Top => (await ranker.AtEdgeAsync(item.ProjectId, columnId, item.Id, atTop: true, ct), "moved to top"),
                CardPlacement.Before before when await ranker.BeforeCardAsync(item, columnId, before.WorkItemKey, ct) is { } between =>
                    (between, $"moved above {before.WorkItemKey}"),
                _ => (await ranker.AtEdgeAsync(item.ProjectId, columnId, item.Id, atTop: false, ct), "moved to bottom"),
            };
            if (rank.Length <= Rank.MaxLength || attempt > 0)
            {
                return (rank, note);
            }

            // The gap is exhausted: renumber the column (order unchanged) and try again.
            await rebalancer.RebalanceColumnAsync(item.ProjectId, columnId, ct);
        }
    }

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
        return new CardView(item.Key, item.Title, item.Priority, item.StatusId, subtasks.Count(doneIds.Contains),
            subtasks.Count, item.RowVersion);
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
