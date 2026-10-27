using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Work.Contracts;

namespace Upms.Application.Work;

internal sealed class WorkItemTitles(IAppDbContext db) : IWorkItemTitles
{
    public async Task<IReadOnlyDictionary<long, WorkItemTitle>> GetAsync(IReadOnlyCollection<long> workItemIds, CancellationToken ct)
    {
        if (workItemIds.Count == 0)
        {
            return new Dictionary<long, WorkItemTitle>();
        }

        var ids = workItemIds.Distinct().ToList();
        return await db.WorkItems.AsNoTracking().IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new WorkItemTitle(w.Id, w.Key, w.Title))
            .ToDictionaryAsync(w => w.Id, ct);
    }
}
