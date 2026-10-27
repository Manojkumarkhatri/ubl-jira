using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Saved filters (Phase 4 FR-039–FR-041, research R17). Sharing is a row per project, checked when read: a filter
/// reaches a project's members only while its owner is active and can still see the project, so leaving the project or
/// being deactivated ends the sharing with no clean-up.</summary>
internal sealed class SavedFilterService(
    IAppDbContext db,
    ICallerContext caller,
    IProjectAccess access,
    IProjectTeam team,
    IUserDirectory users,
    TimeProvider time) : ISavedFilterService
{
    public async Task<Result<SavedFilterList>> ListAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var mine = await db.SavedFilters.AsNoTracking().Include(f => f.Shares)
            .Where(f => f.OwnerId == me.UserId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
        var shared = await SharedWithAsync(me.UserId, null, ct);
        return new SavedFilterList(await ViewsAsync(me.UserId, mine, ct),
            await ViewsAsync(me.UserId, shared.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), ct));
    }

    public async Task<Result<SavedFilterView>> GetAsync(long filterId, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var filter = await db.SavedFilters.AsNoTracking().Include(f => f.Shares).SingleOrDefaultAsync(f => f.Id == filterId, ct);
        if (filter is null || (filter.OwnerId != me.UserId && (await SharedWithAsync(me.UserId, filterId, ct)).Count == 0))
        {
            return AppError.NotFound("filter");
        }

        return (await ViewsAsync(me.UserId, [filter], ct))[0];
    }

    public async Task<Result<SavedFilterView>> SaveAsync(string name, string query, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var created = SavedFilter.Create(me.UserId, name, query, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var filter = created.Value!;
        if (await TakenNameAsync(me.UserId, filter.NormalizedName, null, ct) is { } taken)
        {
            return NameTaken(taken);
        }

        db.SavedFilters.Add(filter);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return NameTaken(filter.Name); // saved twice at once
        }

        return (await ViewsAsync(me.UserId, [filter], ct))[0];
    }

    public Task<Result<SavedFilterView>> RenameAsync(long filterId, string name, byte[] expectedVersion, CancellationToken ct) =>
        ChangeAsync(filterId, expectedVersion, async (filter, me) =>
        {
            if (await TakenNameAsync(me, SavedFilter.Normalize(name), filter.Id, ct) is { } taken)
            {
                return NameTaken(taken);
            }

            return filter.Rename(name, time.GetUtcNow())?.ToAppError();
        }, ct);

    public Task<Result<SavedFilterView>> UpdateAsync(long filterId, string query, byte[] expectedVersion, CancellationToken ct) =>
        ChangeAsync(filterId, expectedVersion, (filter, _) => Task.FromResult(filter.Update(query, time.GetUtcNow())?.ToAppError()), ct);

    public async Task<Result> DeleteAsync(long filterId, CancellationToken ct)
    {
        var (filter, _, error) = await LoadOwnAsync(filterId, ct);
        if (error is not null)
        {
            return error;
        }

        db.SavedFilters.Remove(filter!);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<SavedFilterView>> ShareAsync(long filterId, IReadOnlyList<string> projectKeys, CancellationToken ct)
    {
        var (filter, me, error) = await LoadOwnAsync(filterId, ct);
        if (error is not null)
        {
            return error;
        }

        var projectIds = new List<long>();
        foreach (var key in projectKeys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Members see their projects, administrators every project; anyone else is told the same as for no project
            // (hidden, as everywhere, but with a code that says why).
            var project = await access.RequireAsync(key, ProjectRight.View, ct);
            if (!project.IsSuccess)
            {
                return new AppError(ErrorKind.NotFound, ErrorCodes.NotAMember,
                    $"You can share a filter only with spaces you are a member of; {key.ToUpperInvariant()} is not one of them.");
            }

            projectIds.Add(project.Value!.ProjectId);
        }

        filter!.ShareWith(projectIds, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return (await ViewsAsync(me, [filter], ct))[0];
    }

    private async Task<Result<SavedFilterView>> ChangeAsync(long filterId, byte[] expectedVersion,
        Func<SavedFilter, Guid, Task<AppError?>> change, CancellationToken ct)
    {
        var (filter, me, error) = await LoadOwnAsync(filterId, ct);
        if (error is not null)
        {
            return error;
        }

        var current = (await ViewsAsync(me, [filter!], ct))[0];
        if (!filter!.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return AppError.Conflict("This filter was changed in another window. Its current version is shown.", current);
        }

        if (await change(filter, me) is { } refused)
        {
            return refused;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppError.Conflict("This filter was changed in another window. Its current version is shown.", current);
        }
        catch (DbUpdateException)
        {
            return NameTaken(filter.Name);
        }

        return (await ViewsAsync(me, [filter], ct))[0];
    }

    /// <summary>The caller's own filter, tracked; a filter merely shared with them is <c>Forbidden</c> to change, any other
    /// is not found.</summary>
    private async Task<(SavedFilter? Filter, Guid Me, AppError? Error)> LoadOwnAsync(long filterId, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return (null, Guid.Empty, AppError.Forbidden());
        }

        var filter = await db.SavedFilters.Include(f => f.Shares).SingleOrDefaultAsync(f => f.Id == filterId, ct);
        if (filter is null)
        {
            return (null, me.UserId, AppError.NotFound("filter"));
        }

        if (filter.OwnerId != me.UserId)
        {
            return (null, me.UserId, (await SharedWithAsync(me.UserId, filterId, ct)).Count > 0
                ? AppError.Forbidden("Only its owner can change a filter.")
                : AppError.NotFound("filter"));
        }

        return (filter, me.UserId, null);
    }

    /// <summary>Other people's filters shared with a project within the caller's reach, whose owner can still see it.</summary>
    private async Task<List<SavedFilter>> SharedWithAsync(Guid me, long? onlyFilterId, CancellationToken ct)
    {
        // The "Shared with me" list holds filters shared with the caller's own projects; one filter can be opened from any
        // project the caller can see, so administrators can run a filter shared with any project (null: every project).
        var reach = onlyFilterId is null ? await access.MemberProjectIdsAsync(ct) : await access.VisibleProjectIdsAsync(ct);
        if (reach is { Count: 0 })
        {
            return [];
        }

        var reachIds = reach?.ToList();
        var query = db.SavedFilters.AsNoTracking().Include(f => f.Shares)
            .Where(f => f.OwnerId != me && (onlyFilterId == null || f.Id == onlyFilterId));
        query = reachIds is null ? query.Where(f => f.Shares.Any()) : query.Where(f => f.Shares.Any(s => reachIds.Contains(s.ProjectId)));
        var candidates = await query.ToListAsync(ct);
        var valid = new HashSet<long>();
        foreach (var project in candidates.SelectMany(f => f.Shares.Where(s => reach is null || reach.Contains(s.ProjectId)).Select(s => (Filter: f, s.ProjectId)))
            .GroupBy(x => x.ProjectId))
        {
            var seeing = await team.WhoCanSeeAsync(project.Key, project.Select(x => x.Filter.OwnerId).Distinct().ToList(), ct);
            valid.UnionWith(project.Where(x => seeing.Contains(x.Filter.OwnerId)).Select(x => x.Filter.Id));
        }

        return candidates.Where(f => valid.Contains(f.Id)).ToList();
    }

    private async Task<IReadOnlyList<SavedFilterView>> ViewsAsync(Guid me, IReadOnlyList<SavedFilter> filters, CancellationToken ct)
    {
        var owners = await users.GetAsync(filters.Select(f => f.OwnerId).Distinct().ToList(), ct);
        var projects = (await team.VisibleAmongAsync(me, filters.SelectMany(f => f.Shares.Select(s => s.ProjectId)).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        return filters.Select(f => new SavedFilterView(f.Id, f.Name, f.Query, WorkItemReads.NameOf(owners, f.OwnerId), f.OwnerId == me,
                f.Shares.Where(s => projects.ContainsKey(s.ProjectId)).Select(s => projects[s.ProjectId]).OrderBy(p => p.Key).ToList(),
                f.RowVersion))
            .ToList();
    }

    /// <summary>The name, as its owner wrote it, of another filter of theirs with the same normalized name.</summary>
    private Task<string?> TakenNameAsync(Guid owner, string normalizedName, long? except, CancellationToken ct) =>
        db.SavedFilters.Where(f => f.OwnerId == owner && f.NormalizedName == normalizedName && (except == null || f.Id != except))
            .Select(f => f.Name).FirstOrDefaultAsync(ct);

    private static AppError NameTaken(string name) =>
        AppError.Rule(ErrorCodes.FilterNameTaken, $"You already have a filter called {name}.");
}
