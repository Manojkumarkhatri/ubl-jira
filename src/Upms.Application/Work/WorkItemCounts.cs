using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Work.Contracts;

namespace Upms.Application.Work;

internal sealed class WorkItemCounts(IAppDbContext db) : IWorkItemCounts
{
    public async Task<IReadOnlyDictionary<long, int>> CountByStatusAsync(IReadOnlyCollection<long> statusIds, CancellationToken ct)
    {
        if (statusIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var ids = statusIds.ToList();
        return await db.WorkItems.AsNoTracking()
            .Where(w => ids.Contains(w.StatusId))
            .GroupBy(w => w.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StatusId, x => x.Count, ct);
    }
}
