using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upms.Application.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Indexes items that have no search words yet, in order of their ids, 500 at a time (Phase 4 research R15): items
/// from before the upgrade, and any made where no one indexed them. Run by a background worker.</summary>
public interface ISearchIndexBackfill
{
    /// <returns>How many items were looked at; 0 once every item is.</returns>
    Task<int> IndexPendingAsync(CancellationToken ct);
}

/// <summary>Writes an item's search words from its title, its description's plain text and its live comments' plain text
/// (Phase 4 research R15), in the caller's unit of work, so they change in the same transaction as the text. It never
/// changes the item itself, so indexing never changes the version an open drawer holds.</summary>
public sealed partial class SearchIndexer(IAppDbContext db, ILogger<SearchIndexer> logger) : ISearchIndexBackfill
{
    public const int BatchSize = 500;

    /// <summary>Brings the item's words up to date with what the unit of work holds; the caller saves.</summary>
    public async Task IndexAsync(WorkItem item, CancellationToken ct)
    {
        var saved = await db.Comments.AsNoTracking()
            .Where(c => c.WorkItemId == item.Id)
            .Select(c => new { c.Id, c.BodyText, c.IsDeleted })
            .ToListAsync(ct);
        var comments = saved.ToDictionary(c => c.Id, c => c.IsDeleted ? null : c.BodyText);
        foreach (var pending in db.Comments.Local.Where(c => c.WorkItemId == item.Id))
        {
            comments[pending.Id] = pending.IsDeleted ? null : pending.BodyText;
        }

        var words = SearchWords.Of(string.Join('\n', [item.Title, item.DescriptionText ?? "", .. comments.Values.OfType<string>()]))
            .ToHashSet(StringComparer.Ordinal);
        var existing = await db.SearchTerms.Where(t => t.WorkItemId == item.Id).ToListAsync(ct);
        db.SearchTerms.RemoveRange(existing.Where(t => !words.Contains(t.Term)));
        var kept = existing.Select(t => t.Term).ToHashSet(StringComparer.Ordinal);
        db.SearchTerms.AddRange(words.Where(w => !kept.Contains(w)).Select(w => SearchTerm.For(item.Id, item.ProjectId, w)));
    }

    public async Task<int> IndexPendingAsync(CancellationToken ct)
    {
        // The progress row is held for the batch, so two instances never index the same items.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var last = (await db.Database.SqlQuery<long>($"""
            IF NOT EXISTS (SELECT 1 FROM [SearchIndexProgress] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {SearchIndexProgress.SingleId})
                INSERT INTO [SearchIndexProgress] ([Id], [LastWorkItemId]) VALUES ({SearchIndexProgress.SingleId}, 0);
            SELECT [LastWorkItemId] AS [Value] FROM [SearchIndexProgress] WITH (UPDLOCK) WHERE [Id] = {SearchIndexProgress.SingleId};
            """).ToListAsync(ct)).Single();

        // The next items by id, deleted ones too, so they are found again once restored. Items already indexed when they
        // were written are only passed over.
        var ids = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.Id > last)
            .OrderBy(w => w.Id)
            .Select(w => w.Id)
            .Take(BatchSize)
            .ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var items = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => ids.Contains(w.Id) && !db.SearchTerms.Any(t => t.WorkItemId == w.Id))
            .ToListAsync(ct);
        foreach (var item in items)
        {
            await IndexAsync(item, ct);
        }

        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE [SearchIndexProgress] SET [LastWorkItemId] = {ids[^1]} WHERE [Id] = {SearchIndexProgress.SingleId}", ct);
        await transaction.CommitAsync(ct);
        if (items.Count > 0)
        {
            LogIndexed(logger, items.Count);
        }

        return ids.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Indexed {Count} work items for search.")]
    private static partial void LogIndexed(ILogger logger, int count);
}
