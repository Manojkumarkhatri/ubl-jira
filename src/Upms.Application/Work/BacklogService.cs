using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The backlog of a Scrum project (Phase 3 FR-021, FR-022, FR-024, FR-026; research R8, R10). The backlog and
/// the sprints read one order, the cards' project-wide rank, which the board also uses.</summary>
#pragma warning disable CA1309 // rank comparisons run in SQL with the column's binary collation (research R14)
internal sealed class BacklogService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    CardRanker ranker,
    RankRebalancer rebalancer,
    SprintReads reads,
    TimeProvider time) : IBacklogService
{
    public const int PageSize = 100;

    public async Task<Result<BacklogView>> GetAsync(string projectKey, PageRequest backlogPage, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (caller.Template != ProjectTemplate.Scrum)
        {
            return AppError.NotFound("backlog"); // only Scrum projects have one (contracts/ui-routes.md)
        }

        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var sprints = await db.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == caller.ProjectId)
            .OrderBy(s => s.Number)
            .ToListAsync(ct);
        SprintSection? active = null;
        var planned = new List<SprintSection>();
        foreach (var sprint in sprints.Where(s => s.State != SprintState.Completed))
        {
            var section = await reads.SectionAsync(sprint, info.Statuses, ct);
            if (sprint.State == SprintState.Active)
            {
                active = section;
            }
            else
            {
                planned.Add(section);
            }
        }

        var page = new PageRequest(backlogPage.Page, PageSize);
        var backlog = reads.BacklogCards(caller.ProjectId);
        var total = await backlog.CountAsync(ct);
        var rows = await reads.RowsAsync(backlog.OrderBy(w => w.Rank).ThenBy(w => w.Id).Skip(page.Skip).Take(PageSize),
            caller.ProjectId, info.Statuses, ct);
        var completed = sprints.Where(s => s.State == SprintState.Completed)
            .OrderByDescending(s => s.Number)
            .Select(s => new CompletedSprintLink(s.Number, s.Name, s.EndDate))
            .ToList();
        return new BacklogView(info.Key, info.Name, caller.CanContribute, caller.CanManage, caller.IsAdministrator, active, planned,
            new Page<BacklogRow>(rows, total, page.Page, PageSize), completed,
            info.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(), caller.WorkTypes);
    }

    public async Task<Result<BacklogRow>> MoveAsync(string workItemKey, BacklogTarget target, BacklogPlacement placement,
        byte[] expectedVersion, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("work item");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (allowed.Value!.Template != ProjectTemplate.Scrum)
        {
            return AppError.NotFound("backlog");
        }

        if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(item, ct);
        }

        long? targetId = target is BacklogTarget.Sprint into ? into.SprintId : null;
        var sprints = await db.Sprints
            .Where(s => s.ProjectId == item.ProjectId && (s.Id == item.SprintId || s.Id == targetId))
            .ToListAsync(ct);
        var current = sprints.SingleOrDefault(s => s.Id == item.SprintId);
        Sprint? destination = null;
        if (targetId is { } id)
        {
            destination = sprints.SingleOrDefault(s => s.Id == id);
            if (destination is null)
            {
                return AppError.NotFound("sprint");
            }
        }

        var context = ChangeContext.New(allowed.Value.UserId, time.GetUtcNow());
        // The card's list now: an open card in a completed sprint is in the backlog.
        var fromList = current is { State: not SprintState.Completed } ? current : null;
        var sameList = fromList?.Id == destination?.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (item.JoinSprint(current?.Ref, destination?.Ref, context) is { } refused)
        {
            return refused.ToAppError();
        }

        var list = destination is null ? reads.BacklogCards(item.ProjectId) : reads.SprintCards(item.ProjectId, destination.Id);
        list = list.Where(w => w.Id != item.Id);
        var (rank, note) = await RankInListAsync(item, list, placement, ct);
        if (sameList && rank != item.Rank)
        {
            var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
            var status = statuses.Single(s => s.Id == item.StatusId);
            var statusRef = new StatusRef(status.Id, status.Name, status.Category);
            var oldPosition = await PositionAsync(list, item.Rank, ct);
            var newPosition = await PositionAsync(list, rank, ct);
            item.Move(statusRef, statusRef, rank, oldPosition, newPosition, note, context);
        }
        else if (rank != item.Rank)
        {
            item.Rerank(rank); // a move to another list: its Sprint entry says where it went
        }

        if (!sameList)
        {
            if (fromList is { State: SprintState.Active })
            {
                reads.Record(fromList, item, SprintScopeKind.Removed, context);
            }

            if (destination is { State: SprintState.Active })
            {
                reads.Record(destination, item, SprintScopeKind.Added, context);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            await db.Entry(item).ReloadAsync(ct);
            return await ConflictAsync(item, ct);
        }

        return await RowAsync(item, ct);
    }

    /// <summary>A rank for the card in the target list (research R8): between neighbours of the whole project, so the
    /// board reads the same order. Keep, and any placement in an empty list, keep the card's rank.</summary>
    private async Task<(string Rank, string? Note)> RankInListAsync(WorkItem item, IQueryable<WorkItem> list,
        BacklogPlacement placement, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (rank, note) = placement switch
            {
                BacklogPlacement.Top when await list.MinAsync(w => (string?)w.Rank, ct) is { } first =>
                    (await ranker.BeforeInProjectAsync(item.ProjectId, item.Id, first, ct), "moved to top"),
                BacklogPlacement.Bottom when await list.MaxAsync(w => (string?)w.Rank, ct) is { } last =>
                    (await ranker.AfterInProjectAsync(item.ProjectId, item.Id, last, ct), "moved to bottom"),
                BacklogPlacement.Up when await list.Where(w => string.Compare(w.Rank, item.Rank) < 0).MaxAsync(w => (string?)w.Rank, ct) is { } previous =>
                    (await ranker.BeforeInProjectAsync(item.ProjectId, item.Id, previous, ct), "moved up"),
                BacklogPlacement.Down when await list.Where(w => string.Compare(w.Rank, item.Rank) > 0).MinAsync(w => (string?)w.Rank, ct) is { } next =>
                    (await ranker.AfterInProjectAsync(item.ProjectId, item.Id, next, ct), "moved down"),
                BacklogPlacement.Before before when await list.Where(w => w.Key == before.WorkItemKey).Select(w => w.Rank).FirstOrDefaultAsync(ct) is { } targetRank =>
                    (await ranker.BeforeInProjectAsync(item.ProjectId, item.Id, targetRank, ct), $"moved above {before.WorkItemKey}"),
                _ => (item.Rank, null),
            };
            if (rank.Length <= Rank.MaxLength || attempt > 0)
            {
                return (rank, note);
            }

            // The gap is exhausted: renumber the whole project (order unchanged) and try again.
            await rebalancer.RebalanceProjectAsync(item.ProjectId, ct);
        }
    }

    /// <summary>The 1-based position a rank takes in the list.</summary>
    private static async Task<int> PositionAsync(IQueryable<WorkItem> list, string rank, CancellationToken ct) =>
        await list.CountAsync(w => string.Compare(w.Rank, rank) < 0, ct) + 1;

    private async Task<Result<BacklogRow>> RowAsync(WorkItem item, CancellationToken ct)
    {
        var statuses = await workflow.StatusesAsync(item.ProjectId, ct);
        var rows = await reads.RowsAsync(db.WorkItems.AsNoTracking().Where(w => w.Id == item.Id), item.ProjectId, statuses, ct);
        return rows.Single();
    }

    private async Task<AppError> ConflictAsync(WorkItem item, CancellationToken ct) =>
        AppError.Conflict($"{item.Key} was changed by someone else. The backlog now shows it as it is.", (await RowAsync(item, ct)).Value);
}
#pragma warning restore CA1309
