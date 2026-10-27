using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>Each item's search words follow its title, description and comments in the same transaction, and a worker
/// indexes items that have none yet (Phase 4 research R15).</summary>
public sealed class SearchIndexTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private Task<List<string>> TermsAsync(string key) =>
        QueryAsync(db => db.SearchTerms.AsNoTracking()
            .Where(t => db.WorkItems.IgnoreQueryFilters().Any(w => w.Id == t.WorkItemId && w.Key == key))
            .OrderBy(t => t.Term)
            .Select(t => t.Term)
            .ToListAsync(Ct));

    [Fact]
    public async Task A_new_item_is_indexed_by_its_title()
    {
        await CreateAsync(WorkItemType.Task, "Refund to card");
        await AddAsync(ToDo, "Board task");

        Assert.Equal(["card", "refund", "to"], await TermsAsync("WEB-1"));
        Assert.Equal(["board", "task"], await TermsAsync("WEB-2"));
    }

    [Fact]
    public async Task Title_description_and_comments_change_the_words()
    {
        await CreateAsync(WorkItemType.Task, "Refund to card");

        await EditAsync("WEB-1", new WorkItemEdit.Title("Refunds for wallets"));
        await EditAsync("WEB-1", new WorkItemEdit.Description(Of(Paragraph(Run("Use the "), Run("ledger", "bold")))));
        var comment = (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Text("Chargeback rules"), Ct))).ValueOrThrow();

        Assert.Equal(["chargeback", "for", "ledger", "refunds", "rules", "the", "use", "wallets"], await TermsAsync("WEB-1"));

        var edited = (await CallAsync<ICommentService, Result<CommentView>>(s => s.EditAsync(comment.Id, Text("Dispute rules"), comment.Version, Ct))).ValueOrThrow();
        Assert.Contains("dispute", await TermsAsync("WEB-1"));
        Assert.DoesNotContain("chargeback", await TermsAsync("WEB-1"));

        Assert.True((await CallAsync<ICommentService, Result>(s => s.DeleteAsync(edited.Id, Ct))).IsSuccess);
        Assert.DoesNotContain("dispute", await TermsAsync("WEB-1"));
    }

    [Fact]
    public async Task A_comment_does_not_change_the_items_version()
    {
        await CreateAsync(WorkItemType.Task, "Refund to card");
        var before = (await RequireDetailsAsync("WEB-1")).Version;

        await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Text("Looks good"), Ct));

        Assert.Equal(before, (await RequireDetailsAsync("WEB-1")).Version);
    }

    [Fact]
    public async Task The_worker_indexes_items_without_words_500_at_a_time()
    {
        var board = await BoardAsync();
        var projectId = await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.Id).SingleAsync(Ct));
        var toDo = new StatusRef(board.Columns[0].Id, board.Columns[0].Name, board.Columns[0].Category);
        for (var i = 0; i < 502; i++)
        {
            await Data.WorkItemAsync(projectId, "WEB", toDo, $"Imported item {i}", Rank.After(null) + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(500, await CallAsync<ISearchIndexBackfill, int>(s => s.IndexPendingAsync(Ct)));
        Assert.Equal(2, await CallAsync<ISearchIndexBackfill, int>(s => s.IndexPendingAsync(Ct)));
        Assert.Equal(0, await CallAsync<ISearchIndexBackfill, int>(s => s.IndexPendingAsync(Ct)));
        Assert.Equal(["501", "imported", "item"], await TermsAsync("WEB-502")); // binary order: digits first
    }

    [Fact]
    public async Task Indexing_never_changes_an_items_version()
    {
        await CreateAsync(WorkItemType.Task, "Refund to card");
        var board = await BoardAsync();
        var projectId = await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.Id).SingleAsync(Ct));
        var toDo = new StatusRef(board.Columns[0].Id, board.Columns[0].Name, board.Columns[0].Category);
        await Data.WorkItemAsync(projectId, "WEB", toDo, "Imported refund", Rank.After(null) + "9999");
        var before = await QueryAsync(db => db.WorkItems.AsNoTracking().OrderBy(w => w.Id).Select(w => w.RowVersion).ToListAsync(Ct));

        Assert.Equal(2, await CallAsync<ISearchIndexBackfill, int>(s => s.IndexPendingAsync(Ct)));
        var version = (await RequireDetailsAsync("WEB-1")).Version;
        (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Text("Chargeback rules"), Ct))).ValueOrThrow();

        // So an open drawer can still save with the version it read.
        Assert.Equal(before, await QueryAsync(db => db.WorkItems.AsNoTracking().OrderBy(w => w.Id).Select(w => w.RowVersion).ToListAsync(Ct)));
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Priority(Priority.High), version)).IsSuccess);
        Assert.Equal(["imported", "refund"], await TermsAsync("WEB-2"));
    }
}
