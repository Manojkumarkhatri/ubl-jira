using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Sprints of Scrum projects (Phase 3 FR-023, FR-025, FR-028, FR-029; research R9–R11). Creating, starting,
/// completing and deleting take the project's planning lock, so sprint numbers and the one active sprint hold when two
/// people act at once.</summary>
internal sealed class SprintService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IPlanLock planLock,
    SprintReads reads,
    TimeProvider time) : ISprintService
{
    public async Task<Result<SprintSection>> CreateAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (caller.Template != ProjectTemplate.Scrum)
        {
            return AppError.NotFound("space");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(caller.ProjectId, ct);
        var number = await db.Sprints.Where(s => s.ProjectId == caller.ProjectId).MaxAsync(s => (int?)s.Number, ct) ?? 0;
        var sprint = Sprint.Create(caller.ProjectId, caller.Key, number + 1, ChangeContext.New(caller.UserId, time.GetUtcNow()));
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await SectionAsync(sprint, ct);
    }

    public async Task<Result<SprintSection>> UpdateAsync(long sprintId, string name, string? goal, byte[] expectedVersion,
        CancellationToken ct)
    {
        var loaded = await LoadAsync(sprintId, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var sprint = loaded.Value!.Sprint;
        if (!sprint.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(sprint, ct);
        }

        if (sprint.Update(name, goal) is { } refused)
        {
            return refused.ToAppError();
        }

        return await SaveAsync(sprint, ct);
    }

    public async Task<Result<SprintSection>> StartAsync(long sprintId, DateOnly start, DateOnly? end, string? goal,
        byte[] expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadAsync(sprintId, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (sprint, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(sprint.ProjectId, ct);
        await db.Entry(sprint).ReloadAsync(ct); // as it is now that no other planning change is under way
        if (!sprint.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(sprint, ct);
        }

        if (sprint.State == SprintState.Planned
            && await db.Sprints.AnyAsync(s => s.ProjectId == sprint.ProjectId && s.State == SprintState.Active, ct))
        {
            return AppError.Rule(ErrorCodes.SprintAlreadyActive,
                "Only one sprint can be active at a time. Complete the active sprint before starting another one.");
        }

        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        if (sprint.Start(start, end, goal, context.At) is { } refused)
        {
            return refused.ToAppError();
        }

        // What the team committed to: each card in the sprint, with its estimate now (research R11).
        foreach (var card in await CardsOfAsync(sprint, ct))
        {
            reads.Record(sprint, card, SprintScopeKind.Committed, context);
        }

        return await SaveAsync(sprint, ct, transaction);
    }

    public async Task<Result<SprintSection>> CompleteAsync(long sprintId, long? moveOpenItemsToSprintId, byte[] expectedVersion,
        CancellationToken ct)
    {
        var loaded = await LoadAsync(sprintId, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (sprint, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(sprint.ProjectId, ct);
        await db.Entry(sprint).ReloadAsync(ct);
        if (!sprint.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(sprint, ct);
        }

        Sprint? destination = null;
        if (moveOpenItemsToSprintId is { } destinationId)
        {
            destination = await db.Sprints.SingleOrDefaultAsync(s => s.Id == destinationId && s.ProjectId == sprint.ProjectId, ct);
            if (destination is not { State: SprintState.Planned })
            {
                return AppError.Validation("Destination", "Choose the backlog or a planned sprint for the open items.");
            }
        }

        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        var active = sprint.Ref; // the cards leave it while it is still active
        if (sprint.Complete(context.At) is { } refused)
        {
            return refused.ToAppError();
        }

        // Done cards stay in the completed sprint; open ones go to the backlog or the chosen sprint (FR-028).
        foreach (var card in await CardsOfAsync(sprint, ct))
        {
            var done = card.ResolvedAt is not null;
            reads.Record(sprint, card, done ? SprintScopeKind.Completed : SprintScopeKind.NotCompleted, context);
            if (!done)
            {
                card.JoinSprint(active, destination?.Ref, context);
            }
        }

        return await SaveAsync(sprint, ct, transaction);
    }

    public async Task<Result> DeleteAsync(long sprintId, byte[] expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadAsync(sprintId, ct);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var (sprint, caller) = loaded.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await planLock.AcquireAsync(sprint.ProjectId, ct);
        await db.Entry(sprint).ReloadAsync(ct);
        if (!sprint.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return await ConflictAsync(sprint, ct);
        }

        if (sprint.CheckDeletable() is { } refused)
        {
            return refused.ToAppError();
        }

        // Its cards return to the backlog, keeping their order (FR-023).
        var context = ChangeContext.New(caller.UserId, time.GetUtcNow());
        var cards = await db.WorkItems.IgnoreQueryFilters().Where(w => w.SprintId == sprint.Id).ToListAsync(ct);
        foreach (var card in cards)
        {
            card.JoinSprint(sprint.Ref, null, context);
        }

        db.Sprints.Remove(sprint);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppError.Conflict($"{sprint.Name} was changed by someone else. Its current state is shown.");
        }

        return Result.Ok();
    }

    public async Task<Result<SprintReport>> GetReportAsync(string projectKey, int sprintNumber, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var sprint = await db.Sprints.AsNoTracking()
            .SingleOrDefaultAsync(s => s.ProjectId == caller.ProjectId && s.Number == sprintNumber && s.State == SprintState.Completed, ct);
        if (sprint is null)
        {
            return AppError.NotFound("sprint report");
        }

        var lines = await (
                from e in db.SprintScopeEvents.AsNoTracking()
                join w in db.WorkItems.IgnoreQueryFilters() on e.WorkItemId equals w.Id
                where e.SprintId == sprint.Id
                orderby e.Id
                select new { e.Kind, Line = new ReportLine(w.Key, w.Type, w.Title, e.Estimate, w.IsDeleted) })
            .ToListAsync(ct);
        ReportGroup Group(SprintScopeKind kind)
        {
            var items = lines.Where(l => l.Kind == kind).Select(l => l.Line).ToList();
            return new ReportGroup(items, items.Sum(i => i.Estimate ?? 0));
        }

        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        return new SprintReport(info.Key, info.Name, sprint.Name, sprint.Goal, sprint.StartDate!.Value, sprint.EndDate!.Value,
            sprint.CompletedAt!.Value, Group(SprintScopeKind.Committed), Group(SprintScopeKind.Added), Group(SprintScopeKind.Removed),
            Group(SprintScopeKind.Completed), Group(SprintScopeKind.NotCompleted), caller.CanManage, caller.IsAdministrator);
    }

    /// <summary>The sprint and the caller, who needs the Manage right in its project; sprints exist in Scrum projects
    /// only.</summary>
    private async Task<Result<(Sprint Sprint, ProjectAccessInfo Caller)>> LoadAsync(long sprintId, CancellationToken ct)
    {
        var sprint = await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint is null)
        {
            return AppError.NotFound("sprint");
        }

        var allowed = await access.RequireAsync(sprint.ProjectId, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        return (sprint, allowed.Value!);
    }

    /// <summary>The sprint's live cards, tracked for changes.</summary>
    private Task<List<WorkItem>> CardsOfAsync(Sprint sprint, CancellationToken ct) =>
        db.WorkItems
            .Where(w => w.SprintId == sprint.Id && w.ParentId == null
                && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug))
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);

    private async Task<Result<SprintSection>> SaveAsync(Sprint sprint, CancellationToken ct,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(sprint).ReloadAsync(ct);
            return await ConflictAsync(sprint, ct);
        }

        return await SectionAsync(sprint, ct);
    }

    private async Task<SprintSection> SectionAsync(Sprint sprint, CancellationToken ct) =>
        await reads.SectionAsync(sprint, await workflow.StatusesAsync(sprint.ProjectId, ct), ct);

    private async Task<AppError> ConflictAsync(Sprint sprint, CancellationToken ct) =>
        AppError.Conflict($"{sprint.Name} was changed by someone else. Its current state is shown.", await SectionAsync(sprint, ct));
}
