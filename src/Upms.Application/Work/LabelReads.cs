using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reading and filtering by labels, shared by the drawer, board, list and search (Phase 4 FR-045, FR-046).</summary>
internal static class LabelReads
{
    /// <summary>Each item's labels, sorted by name; items without labels are left out.</summary>
    public static async Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> ForItemsAsync(IAppDbContext db, IReadOnlyCollection<long> itemIds,
        CancellationToken ct)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<string>>();
        }

        var ids = itemIds.Distinct().ToList();
        var rows = await db.WorkItemLabels.AsNoTracking()
            .Where(x => ids.Contains(x.WorkItemId))
            .Join(db.Labels, x => x.LabelId, l => l.Id, (x, l) => new { x.WorkItemId, l.Name })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.WorkItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static async Task<IReadOnlyList<string>> ForItemAsync(IAppDbContext db, long itemId, CancellationToken ct) =>
        (await ForItemsAsync(db, [itemId], ct)).GetValueOrDefault(itemId) ?? [];

    /// <summary>Items with any of the labels, whatever their letter case (FR-046).</summary>
    public static async Task<IQueryable<WorkItem>> WithAnyAsync(IAppDbContext db, IQueryable<WorkItem> items, IReadOnlyList<string> labels,
        CancellationToken ct)
    {
        var names = labels.Select(Label.Normalize).Where(n => n.Length > 0).Distinct().ToList();
        var labelIds = await db.Labels.AsNoTracking().Where(l => names.Contains(l.NormalizedName)).Select(l => l.Id).ToListAsync(ct);
        return items.Where(w => db.WorkItemLabels.Any(x => x.WorkItemId == w.Id && labelIds.Contains(x.LabelId)));
    }
}
