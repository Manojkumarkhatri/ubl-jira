using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The search's "resolved" date range, behind the Summary page's "completed" count (Jira parity pass FR-022).</summary>
public sealed class SearchResolvedTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    [Fact]
    public async Task JP_FR022_Resolved_finds_items_completed_in_the_range()
    {
        var early = await AddAsync(ToDo, "Done early");
        (await EditAsync(early.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();
        Harness.Time.Advance(TimeSpan.FromDays(10));
        var late = await AddAsync(ToDo, "Done late");
        (await EditAsync(late.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();
        await AddAsync(ToDo, "Still open");

        var today = DateOnly.FromDateTime(Harness.Time.GetUtcNow().UtcDateTime);
        var found = (await CallAsync<ISearchService, Result<SearchResults>>(s =>
            s.SearchAsync(new SearchQuery(null, ProjectKeys: ["WEB"], Resolved: new DateRange(today.AddDays(-6), today)), PageRequest.First, Ct)))
            .ValueOrThrow();

        Assert.Equal([late.Key], found.Rows.Items.Select(r => r.Key));
    }
}
