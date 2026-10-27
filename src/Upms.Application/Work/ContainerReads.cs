using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;

namespace Upms.Application.Work;

/// <summary>Epics and stages for tags on cards and rows, with an epic's color (Jira parity pass FR-025).</summary>
internal static class ContainerReads
{
    /// <summary>The live containers among the ids; a deleted one is left out, so its items show none.</summary>
    public static async Task<IReadOnlyDictionary<long, ContainerView>> ForIdsAsync(IAppDbContext db, IEnumerable<long> containerIds,
        CancellationToken ct)
    {
        var ids = containerIds.Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<long, ContainerView>()
            : await db.WorkItems.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, View = new ContainerView(c.Key, c.Title, c.Type, c.Color) })
                .ToDictionaryAsync(c => c.Id, c => c.View, ct);
    }
}
