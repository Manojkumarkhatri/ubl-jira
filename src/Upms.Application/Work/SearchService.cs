using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Search across projects (Phase 4 FR-032–FR-038, research R15, R16). Every query starts from the projects the
/// caller can see and the live items in them; each typed word must start one of the item's search words.</summary>
internal sealed class SearchService(
    IAppDbContext db,
    ICallerContext caller,
    IProjectAccess access,
    IProjectTeam team,
    IProjectWorkflow workflow,
    IUserDirectory users,
    TimeProvider time) : ISearchService
{
    public const int RecentShown = 10;

    private static readonly TimeSpan WorkedOnWindow = TimeSpan.FromDays(30);

    public async Task<Result<string>> ResolveKeyAsync(string text, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true })
        {
            return AppError.Forbidden();
        }

        var typed = (text ?? "").Trim().ToUpperInvariant();
        if (!SearchWords.LooksLikeKey(text, out var key))
        {
            return NotFound(typed);
        }

        var projectId = await db.WorkItems.AsNoTracking().Where(w => w.Key == key).Select(w => (long?)w.ProjectId).SingleOrDefaultAsync(ct);
        return projectId is { } id && (await access.RequireAsync(id, ProjectRight.View, ct)).IsSuccess ? key : NotFound(key);
    }

    public async Task<Result<IReadOnlyList<SearchResultRow>>> RecentAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var items = Visible(await access.VisibleProjectIdsAsync(ct));
        var recent = await db.RecentViews.AsNoTracking()
            .Where(r => r.UserId == me.UserId)
            .Join(items, r => r.WorkItemId, w => w.Id, (r, w) => new { r.ViewedAt, Item = w })
            .OrderByDescending(r => r.ViewedAt)
            .Take(RecentShown)
            .Select(r => r.Item)
            .Select(RowSource)
            .ToListAsync(ct);
        return Result<IReadOnlyList<SearchResultRow>>.Ok(await RowsAsync(me.UserId, recent, ct));
    }

    public async Task<Result<Page<SearchResultRow>>> WorkedOnAsync(PageRequest page, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        // What the caller created, changed or commented on lately, read from the history by actor (Jira parity pass FR-022a;
        // research R9). Each item once, at its latest change; the row's time is that change.
        page = page.Normalized();
        var since = time.GetUtcNow() - WorkedOnWindow;
        var latest = db.WorkItemChanges.AsNoTracking()
            .Where(c => c.ActorId == me.UserId && c.OccurredAt >= since)
            .GroupBy(c => c.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, At = g.Max(c => c.OccurredAt) });
        var worked = latest.Join(Visible(await access.VisibleProjectIdsAsync(ct)), l => l.WorkItemId, w => w.Id, (l, w) => new { l.At, Item = w });
        var total = await worked.CountAsync(ct);
        var rows = await worked
            .OrderByDescending(r => r.At).ThenByDescending(r => r.Item.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(r => new
            {
                r.At,
                Row = new RowData(r.Item.Id, r.Item.Key, r.Item.Type, r.Item.Title, r.Item.ProjectId, r.Item.StatusId,
                    r.Item.AssigneeId, r.Item.UpdatedAt, r.Item.ContainerId),
            })
            .ToListAsync(ct);
        var shown = rows.ConvertAll(r => r.Row with { UpdatedAt = r.At });
        return new Page<SearchResultRow>(await RowsAsync(me.UserId, shown, ct), total, page.Page, page.PageSize);
    }

    public async Task<Result<SearchResults>> SearchAsync(SearchQuery query, PageRequest page, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        page = page.Normalized();
        var visible = await access.VisibleProjectIdsAsync(ct);
        var unavailable = new List<string>();
        var items = Visible(visible);

        if (query.ProjectKeys is { Count: > 0 } keys)
        {
            var chosen = new List<long>();
            foreach (var key in keys.Select(k => k.Trim().ToUpperInvariant()).Distinct())
            {
                var project = await access.RequireAsync(key, ProjectRight.View, ct);
                if (project.IsSuccess)
                {
                    chosen.Add(project.Value!.ProjectId);
                }
                else
                {
                    unavailable.Add($"Space {key}"); // filters can outlive access (FR-041)
                }
            }

            items = items.Where(w => chosen.Contains(w.ProjectId));
            visible = chosen.ToHashSet();
        }

        foreach (var word in SearchWords.Query(query.Words))
        {
            items = items.Where(w => db.SearchTerms.Any(t => t.WorkItemId == w.Id && t.Term.StartsWith(word)));
        }

        items = await FilterAsync(items, query, me.UserId, visible, ct);
        if (query.Labels is { Count: > 0 } labels)
        {
            items = await LabelReads.WithAnyAsync(db, items, labels, ct); // Phase 4 FR-046
        }

        var total = await items.CountAsync(ct);
        var rows = await Sort(items, query.Sort, query.Descending)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(RowSource)
            .ToListAsync(ct);
        return new SearchResults(new Page<SearchResultRow>(await RowsAsync(me.UserId, rows, ct), total, page.Page, page.PageSize), unavailable);
    }

    private IQueryable<WorkItem> Visible(IReadOnlySet<long>? visible)
    {
        var items = db.WorkItems.AsNoTracking(); // live items only: deleted ones are filtered out
        if (visible is not null)
        {
            var ids = visible.ToList();
            items = items.Where(w => ids.Contains(w.ProjectId));
        }

        return items;
    }

    private async Task<IQueryable<WorkItem>> FilterAsync(IQueryable<WorkItem> items, SearchQuery query, Guid me,
        IReadOnlySet<long>? projects, CancellationToken ct)
    {
        if (query.Types is { Count: > 0 } types)
        {
            var chosen = types.ToList();
            items = items.Where(w => chosen.Contains(w.Type));
        }

        if (query.Priorities is { Count: > 0 } priorities)
        {
            var chosen = priorities.ToList();
            items = items.Where(w => chosen.Contains(w.Priority));
        }

        if (query.Categories is { Count: > 0 } categories)
        {
            // Status types live with each project's columns; one parameter carries however many columns there are.
            var statuses = (await workflow.StatusIdsAsync(categories, projects?.ToList(), ct)).ToList();
            items = items.Where(w => EF.Parameter(statuses).Contains(w.StatusId));
        }

        if (query.AssignedToMe || query.Unassigned || query.Assignees is { Count: > 0 })
        {
            var people = (query.Assignees ?? []).ToList();
            if (query.AssignedToMe)
            {
                people.Add(me);
            }

            var unassigned = query.Unassigned;
            items = items.Where(w => (unassigned && w.AssigneeId == null) || (w.AssigneeId != null && people.Contains(w.AssigneeId.Value)));
        }

        if (query.Creators is { Count: > 0 } creators)
        {
            var chosen = creators.ToList();
            items = items.Where(w => chosen.Contains(w.CreatedById));
        }

        if (query.Created is { } created)
        {
            var (start, end) = Bounds(created);
            items = items.Where(w => (start == null || w.CreatedAt >= start) && (end == null || w.CreatedAt < end));
        }

        if (query.Updated is { } updated)
        {
            var (start, end) = Bounds(updated);
            items = items.Where(w => (start == null || w.UpdatedAt >= start) && (end == null || w.UpdatedAt < end));
        }

        if (query.Resolved is { } resolved)
        {
            // Completed in the range, for the Summary page's count (Jira parity pass FR-022); items reopened since have no date.
            var (start, end) = Bounds(resolved);
            items = items.Where(w => w.ResolvedAt != null && (start == null || w.ResolvedAt >= start) && (end == null || w.ResolvedAt < end));
        }

        if (query.Due is { From: var dueFrom, To: var dueTo })
        {
            if (dueFrom is { } from)
            {
                items = items.Where(w => w.DueDate >= from);
            }

            if (dueTo is { } to)
            {
                items = items.Where(w => w.DueDate <= to);
            }
        }

        return items;
    }

    /// <summary>Whole UTC days, both ends included: the start of the first day and the start of the day after the last.</summary>
    private static (DateTimeOffset? Start, DateTimeOffset? End) Bounds(DateRange range) =>
        (range.From is { } from ? new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
            range.To is { } to ? new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null);

#pragma warning disable CA1866 // translated to SQL (CHARINDEX), which has no char overload
    private static IQueryable<WorkItem> Sort(IQueryable<WorkItem> items, SearchSort sort, bool descending) => (sort, descending) switch
    {
        (SearchSort.Created, false) => items.OrderBy(w => w.CreatedAt).ThenBy(w => w.Id),
        (SearchSort.Created, true) => items.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id),
        // Keys sort by project key, then by number: WEB-9 before WEB-10.
        (SearchSort.Key, false) => items.OrderBy(w => w.Key.Substring(0, w.Key.IndexOf("-"))).ThenBy(w => w.Number),
        (SearchSort.Key, true) => items.OrderByDescending(w => w.Key.Substring(0, w.Key.IndexOf("-"))).ThenByDescending(w => w.Number),
        (SearchSort.Priority, false) => items.OrderBy(PriorityRank).ThenByDescending(w => w.UpdatedAt).ThenByDescending(w => w.Id),
        (SearchSort.Priority, true) => items.OrderByDescending(PriorityRank).ThenByDescending(w => w.UpdatedAt).ThenByDescending(w => w.Id),
        (SearchSort.DueDate, false) => items.OrderBy(w => w.DueDate == null).ThenBy(w => w.DueDate).ThenBy(w => w.Id),
        (SearchSort.DueDate, true) => items.OrderBy(w => w.DueDate == null).ThenByDescending(w => w.DueDate).ThenByDescending(w => w.Id),
        (_, false) => items.OrderBy(w => w.UpdatedAt).ThenBy(w => w.Id),
        _ => items.OrderByDescending(w => w.UpdatedAt).ThenByDescending(w => w.Id),
    };
#pragma warning restore CA1866

    private static readonly Expression<Func<WorkItem, int>> PriorityRank = w =>
        w.Priority == Priority.Highest ? 0
        : w.Priority == Priority.High ? 1
        : w.Priority == Priority.Medium ? 2
        : w.Priority == Priority.Low ? 3
        : 4;

    private static readonly Expression<Func<WorkItem, RowData>> RowSource = w =>
        new RowData(w.Id, w.Key, w.Type, w.Title, w.ProjectId, w.StatusId, w.AssigneeId, w.UpdatedAt, w.ContainerId);

    /// <summary>Project names, column names and assignees for a page of rows, read once each.</summary>
    private async Task<IReadOnlyList<SearchResultRow>> RowsAsync(Guid me, IReadOnlyList<RowData> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = (await team.VisibleAmongAsync(me, projectIds, ct)).ToDictionary(p => p.Id);
        var statuses = await workflow.StatusesAsync(projectIds, ct);
        var assignees = await users.GetAsync(rows.Where(r => r.AssigneeId is not null).Select(r => r.AssigneeId!.Value).Distinct().ToList(), ct);
        var labels = await LabelReads.ForItemsAsync(db, rows.Select(r => r.Id).ToList(), ct);
        var containers = await ContainerReads.ForIdsAsync(db, rows.Where(r => r.ContainerId != null).Select(r => r.ContainerId!.Value), ct);
        return rows.Where(r => projects.ContainsKey(r.ProjectId))
            .Select(r => new SearchResultRow(r.Key, r.Type, r.Title, projects[r.ProjectId].Key, projects[r.ProjectId].Name,
                WorkItemReads.ToOption(statuses.GetValueOrDefault(r.ProjectId) ?? [], r.StatusId),
                r.AssigneeId is { } id && assignees.TryGetValue(id, out var person) ? AssigneeRef.Of(id, person.DisplayName, person.IsActive) : null,
                labels.GetValueOrDefault(r.Id) ?? [], r.UpdatedAt,
                r.ContainerId is { } containerId ? containers.GetValueOrDefault(containerId) : null))
            .ToList();
    }

    private static AppError NotFound(string key) =>
        new(ErrorKind.NotFound, ErrorCodes.NotFound, $"No work item {key} was found.");

    private sealed record RowData(long Id, string Key, WorkItemType Type, string Title, long ProjectId, long StatusId, Guid? AssigneeId,
        DateTimeOffset UpdatedAt, long? ContainerId);
}
