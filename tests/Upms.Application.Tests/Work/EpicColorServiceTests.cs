using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>An epic's color, changed by contributors and shown on its tags everywhere (Jira parity pass US5 scenario 2,
/// FR-025; research R15).</summary>
public sealed class EpicColorServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    /// <summary>Epic WEB-1 (Purple, as the first epic) with story WEB-2 in it, dated for the timeline.</summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card", "WEB-1");
        (await EditAsync("WEB-2", new WorkItemEdit.Dates(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9)))).ValueOrThrow();
    }

    private async Task<Result<WorkItemDetails>> ColorAsync(string key, EpicColor color) =>
        await EditAsync(key, new WorkItemEdit.Color(color));

    [Fact]
    public async Task JP_US5_AS2_A_contributor_changes_an_epics_color_and_it_is_recorded()
    {
        Assert.Equal(EpicColor.Purple, (await RequireDetailsAsync("WEB-1")).Color);
        ActAs(await MemberAsync("bilal"));

        var changed = await ColorAsync("WEB-1", EpicColor.Teal);

        Assert.True(changed.IsSuccess, changed.Error?.Message);
        Assert.Equal(EpicColor.Teal, changed.Value!.Color);
        var entry = (await HistoryAsync("WEB-1")).Items.Single(c => c.Field == WorkItemField.Color);
        Assert.Equal((WorkItemField.Color, "Purple", "Teal"), (entry.Field, entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task Viewers_cannot_change_a_color()
    {
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        Assert.Equal(ErrorKind.Forbidden, (await ColorAsync("WEB-1", EpicColor.Teal)).Error!.Kind);
    }

    [Fact]
    public async Task Only_epics_have_a_color()
    {
        var refused = await ColorAsync("WEB-2", EpicColor.Teal);

        Assert.Equal(ErrorCodes.ColorNotAllowed, refused.Error!.Code);
        Assert.Null((await RequireDetailsAsync("WEB-2")).Color);
    }

    [Fact]
    public async Task The_color_reaches_board_cards_list_rows_timeline_bars_and_search_results()
    {
        (await ColorAsync("WEB-1", EpicColor.Orange)).ValueOrThrow();

        var card = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == "WEB-2");
        var list = (await CallAsync<IWorkItemListService, Result<WorkItemListView>>(s =>
            s.ListAsync("WEB", new WorkItemListQuery(), new DateOnly(2026, 10, 7), Ct))).ValueOrThrow();
        var timeline = (await CallAsync<ITimelineService, Result<TimelineView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();
        var found = (await CallAsync<ISearchService, Result<SearchResults>>(s =>
            s.SearchAsync(new SearchQuery("refund"), PageRequest.First, Ct))).ValueOrThrow();

        Assert.Equal(EpicColor.Orange, card.Container?.Color);
        Assert.Equal(EpicColor.Orange, list.Rows.Items.Single(r => r.Key == "WEB-2").Container?.Color);
        Assert.Equal(EpicColor.Orange, timeline.Groups.Single(g => g.Container.Key == "WEB-1").Container.Color);
        Assert.Equal(EpicColor.Orange, found.Rows.Items.Single(r => r.Key == "WEB-2").Container?.Color);
    }
}
