using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Dependencies between items of one project (Phase 3 FR-047–FR-052; research R18). Adding takes the planning
/// lock around the loop check, so two opposite links added at once leave one. Both items' history records each link and
/// its removal.</summary>
internal sealed class DependencyService(
    IAppDbContext db,
    IProjectAccess access,
    IPlanLock planLock,
    DependencyReads reads,
    TimeProvider time) : IDependencyService
{
    private const int MaxItems = 20;

    public async Task<Result<DependencyList>> AddAsync(string waitingKey, string waitedForKey, CancellationToken ct)
    {
        var loaded = await LoadPairAsync(waitingKey, waitedForKey, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (waiting, waitedFor, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(waiting.ProjectId, ct);
        var links = await db.Dependencies.AsNoTracking()
            .Where(d => d.ProjectId == waiting.ProjectId)
            .Select(d => new { d.PredecessorId, d.SuccessorId })
            .ToListAsync(ct);
        if (DependencyGraph.CheckNew(Node(waiting), Node(waitedFor), links.ConvertAll(l => (l.PredecessorId, l.SuccessorId))) is { } refused)
        {
            return refused.ToAppError();
        }

        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        db.Dependencies.Add(Dependency.Create(waiting.ProjectId, waitedFor.Id, waiting.Id, context));
        waiting.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitsForText(waitedFor), context);
        waitedFor.RecordActivity(WorkItemField.DependencyAdded, null, DependencyReads.WaitedForByText(waiting), context);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await reads.ListAsync(waiting, null, ct);
    }

    public async Task<Result<DependencyList>> RemoveAsync(string waitingKey, string waitedForKey, CancellationToken ct)
    {
        var loaded = await LoadPairAsync(waitingKey, waitedForKey, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (waiting, waitedFor, caller) = loaded.Value;
        var link = await db.Dependencies.SingleOrDefaultAsync(d => d.PredecessorId == waitedFor.Id && d.SuccessorId == waiting.Id, ct);
        if (link is not null)
        {
            var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
            db.Dependencies.Remove(link);
            waiting.RecordActivity(WorkItemField.DependencyRemoved, DependencyReads.WaitsForText(waitedFor), null, context);
            waitedFor.RecordActivity(WorkItemField.DependencyRemoved, DependencyReads.WaitedForByText(waiting), null, context);
            await db.SaveChangesAsync(ct);
        }

        return await reads.ListAsync(waiting, null, ct);
    }

    public async Task<Result<IReadOnlyList<ItemOption>>> FindItemsAsync(string projectKey, string term, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var text = term.Trim();
        if (text.Length == 0)
        {
            return Result<IReadOnlyList<ItemOption>>.Ok([]);
        }

        var key = text.ToUpperInvariant();
        var items = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == allowed.Value!.ProjectId && (w.Key == key || w.Title.Contains(text)))
            .OrderBy(w => w.Key == key ? 0 : 1).ThenByDescending(w => w.Number)
            .Take(MaxItems)
            .Select(w => new ItemOption(w.Key, w.Type, w.Title))
            .ToListAsync(ct);
        return items;
    }

    /// <summary>Both items, tracked, in the caller's project with the Contribute right; an item of another project is not
    /// found.</summary>
    private async Task<Result<(WorkItem Waiting, WorkItem WaitedFor, ProjectAccessInfo Caller)>> LoadPairAsync(string waitingKey,
        string waitedForKey, CancellationToken ct)
    {
        var waiting = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == waitingKey, ct);
        if (waiting is null)
        {
            return AppError.NotFound("work item");
        }

        var allowed = await access.RequireAsync(waiting.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var waitedFor = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == waitedForKey && w.ProjectId == waiting.ProjectId, ct);
        return waitedFor is null ? AppError.NotFound("work item") : (waiting, waitedFor, allowed.Value!);
    }

    private static DependencyNode Node(WorkItem item) => new(item.Id, item.ParentId, item.ContainerId);
}
