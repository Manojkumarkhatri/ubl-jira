using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Filters, sorts and pages in SQL over <c>IX_WorkItems_Project_Live</c>. Sorting by status (column position,
/// then board order) or by assignee (display name) needs other modules' data, so those two order the matching items'
/// keys in memory and then read the page; a project holds at most a few thousand items (research R11).</summary>
internal sealed class WorkItemListService(IAppDbContext db, IProjectAccess access, IProjectWorkflow workflow, AssigneeReads assignees)
    : IWorkItemListService
{
    public const int PageSize = 50;
    private const int MaxWords = 10;

    public async Task<Result<WorkItemListView>> ListAsync(string projectKey, WorkItemListQuery query, DateOnly today,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var doneIds = info.Statuses.Where(s => s.Category == StatusCategory.Done).Select(s => s.Id).ToList();
        var matching = Filter(db.WorkItems.AsNoTracking().Where(w => w.ProjectId == info.ProjectId), query, info.Statuses, doneIds,
            caller.UserId, today);
        var page = new PageRequest(query.Page, PageSize).Normalized();
        var team = await assignees.TeamAsync(info.ProjectId, ct);

        int total;
        List<long> pageIds;
        if (query.Sort is ListSort.Status or ListSort.Assignee)
        {
            var keys = await matching.Select(w => new SortKeys(w.Id, w.Number, w.StatusId, w.Rank, w.AssigneeId)).ToListAsync(ct);
            total = keys.Count;
            var ordered = query.Sort == ListSort.Status
                ? OrderByStatus(keys, info.Statuses, query.Descending)
                : OrderByAssignee(keys, await assignees.DescribeAsync(team, keys.Where(k => k.AssigneeId != null).Select(k => k.AssigneeId!.Value), ct),
                    query.Descending);
            pageIds = ordered.Skip(page.Skip).Take(page.PageSize).Select(k => k.Id).ToList();
        }
        else
        {
            total = await matching.CountAsync(ct);
            pageIds = await Sort(matching, query.Sort, query.Descending).Skip(page.Skip).Take(page.PageSize).Select(w => w.Id).ToListAsync(ct);
        }

        var rows = await db.WorkItems.AsNoTracking()
            .Where(w => pageIds.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.Key,
                w.Title,
                ParentKey = db.WorkItems.Where(p => p.Id == w.ParentId).Select(p => p.Key).FirstOrDefault(),
                w.StatusId,
                w.Priority,
                w.AssigneeId,
                w.StartDate,
                w.DueDate,
                w.UpdatedAt,
            })
            .ToDictionaryAsync(r => r.Id, ct);
        var people = await assignees.DescribeAsync(team, rows.Values.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        var items = pageIds.ConvertAll(id =>
        {
            var row = rows[id];
            return new WorkItemRow(row.Key, row.Title, row.ParentKey, WorkItemReads.ToOption(info.Statuses, row.StatusId), row.Priority,
                row.AssigneeId is { } assigneeId ? people[assigneeId] : null, row.StartDate, row.DueDate, row.UpdatedAt,
                !doneIds.Contains(row.StatusId));
        });

        return new WorkItemListView(
            info.Key,
            info.Name,
            caller.CanContribute,
            caller.CanManage,
            caller.IsAdministrator,
            info.Statuses.First(s => s.Category == StatusCategory.ToDo).Id,
            info.Statuses.Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList(),
            await PeopleAsync(info.ProjectId, team, caller.UserId, ct),
            new Page<WorkItemRow>(items, total, page.Page, page.PageSize));
    }

    private static IQueryable<WorkItem> Filter(IQueryable<WorkItem> items, WorkItemListQuery query, IReadOnlyList<StatusInfo> statuses,
        List<long> doneIds, Guid me, DateOnly today)
    {
        if (query.ColumnIds is { Count: > 0 } columns)
        {
            var columnIds = columns.ToList();
            items = items.Where(w => columnIds.Contains(w.StatusId));
        }

        if (query.Categories is { Count: > 0 } categories)
        {
            var statusIds = statuses.Where(s => categories.Contains(s.Category)).Select(s => s.Id).ToList();
            items = items.Where(w => statusIds.Contains(w.StatusId));
        }

        if (query.Priorities is { Count: > 0 } priorities)
        {
            var chosen = priorities.ToList();
            items = items.Where(w => chosen.Contains(w.Priority));
        }

        var people = (query.AssigneeIds ?? []).Concat(query.AssignedToMe ? [me] : []).Distinct().ToList();
        if (people.Count > 0 && query.Unassigned)
        {
            items = items.Where(w => w.AssigneeId == null || people.Contains(w.AssigneeId.Value));
        }
        else if (people.Count > 0)
        {
            items = items.Where(w => w.AssigneeId != null && people.Contains(w.AssigneeId.Value));
        }
        else if (query.Unassigned)
        {
            items = items.Where(w => w.AssigneeId == null);
        }

        var weekEnd = today.AddDays(7);
        items = query.Due switch
        {
            DueFilter.Overdue => items.Where(w => !doneIds.Contains(w.StatusId) && w.DueDate < today),
            DueFilter.Next7Days => items.Where(w => !doneIds.Contains(w.StatusId) && w.DueDate >= today && w.DueDate <= weekEnd),
            DueFilter.NoDueDate => items.Where(w => w.DueDate == null),
            _ => items,
        };

        foreach (var word in (query.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxWords))
        {
            items = items.Where(w => w.Title.Contains(word) || (w.Description != null && w.Description.Contains(word)));
        }

        return items;
    }

    /// <summary>Undated items come last in both directions (FR-029); equal values keep key order in the same direction.</summary>
    private static IQueryable<WorkItem> Sort(IQueryable<WorkItem> items, ListSort sort, bool descending) => (sort, descending) switch
    {
        (ListSort.Title, false) => items.OrderBy(w => w.Title).ThenBy(w => w.Number),
        (ListSort.Title, true) => items.OrderByDescending(w => w.Title).ThenByDescending(w => w.Number),
        (ListSort.Priority, false) => items.OrderBy(PriorityRank).ThenBy(w => w.Number),
        (ListSort.Priority, true) => items.OrderByDescending(PriorityRank).ThenByDescending(w => w.Number),
        (ListSort.StartDate, false) => items.OrderBy(w => w.StartDate == null).ThenBy(w => w.StartDate).ThenBy(w => w.Number),
        (ListSort.StartDate, true) => items.OrderBy(w => w.StartDate == null).ThenByDescending(w => w.StartDate).ThenByDescending(w => w.Number),
        (ListSort.DueDate, false) => items.OrderBy(w => w.DueDate == null).ThenBy(w => w.DueDate).ThenBy(w => w.Number),
        (ListSort.DueDate, true) => items.OrderBy(w => w.DueDate == null).ThenByDescending(w => w.DueDate).ThenByDescending(w => w.Number),
        (ListSort.Updated, false) => items.OrderBy(w => w.UpdatedAt).ThenBy(w => w.Number),
        (ListSort.Updated, true) => items.OrderByDescending(w => w.UpdatedAt).ThenByDescending(w => w.Number),
        (_, false) => items.OrderBy(w => w.Number),
        _ => items.OrderByDescending(w => w.Number),
    };

    // Priorities are stored by name, so their order is spelled out: Highest first when ascending.
    private static readonly System.Linq.Expressions.Expression<Func<WorkItem, int>> PriorityRank = w =>
        w.Priority == Priority.Highest ? 0
        : w.Priority == Priority.High ? 1
        : w.Priority == Priority.Medium ? 2
        : w.Priority == Priority.Low ? 3
        : 4;

    /// <summary>By column position, then board order.</summary>
    private static IEnumerable<SortKeys> OrderByStatus(List<SortKeys> keys, IReadOnlyList<StatusInfo> statuses, bool descending)
    {
        var positions = statuses.ToDictionary(s => s.Id, s => s.Position);
        var ascending = keys
            .OrderBy(k => positions.GetValueOrDefault(k.StatusId, int.MaxValue))
            .ThenBy(k => k.Rank, StringComparer.Ordinal)
            .ThenBy(k => k.Number);
        return descending ? ascending.Reverse() : ascending;
    }

    /// <summary>By display name; unassigned items come last in both directions.</summary>
    private static IEnumerable<SortKeys> OrderByAssignee(List<SortKeys> keys, IReadOnlyDictionary<Guid, AssigneeRef> names, bool descending)
    {
        var assigned = keys.Where(k => k.AssigneeId != null)
            .OrderBy(k => names[k.AssigneeId!.Value].DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(k => k.Number)
            .ToList();
        if (descending)
        {
            assigned.Reverse();
        }

        var unassigned = keys.Where(k => k.AssigneeId == null).OrderBy(k => k.Number).ToList();
        if (descending)
        {
            unassigned.Reverse();
        }

        return assigned.Concat(unassigned);
    }

    /// <summary>The team, plus people who left it but still have tasks here, by name.</summary>
    private async Task<IReadOnlyList<AssigneeOption>> PeopleAsync(long projectId, IReadOnlyList<TeamMemberInfo> team, Guid me,
        CancellationToken ct)
    {
        var assigned = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == projectId && w.AssigneeId != null)
            .Select(w => w.AssigneeId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var described = await assignees.DescribeAsync(team, team.Select(m => m.UserId).Concat(assigned), ct);
        return described.Values
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.UserId)
            .Select(p => new AssigneeOption(p.UserId, p.DisplayName, p.UserId == me))
            .ToList();
    }

    private sealed record SortKeys(long Id, int Number, long StatusId, string Rank, Guid? AssigneeId);
}
