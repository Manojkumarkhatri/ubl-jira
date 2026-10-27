using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>In Scrum projects the cards share one order across the whole project, so the backlog and the board agree
/// (Phase 3 FR-022, research R8). WEB is amina's Scrum project.</summary>
public sealed class ScrumRankTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected override ProjectTemplate Template => ProjectTemplate.Scrum;

    /// <summary>Moves a card with the version its drawer shows (a Scrum board shows only the active sprint).</summary>
    private async Task MoveKeyAsync(string key, long columnId, CardPlacement placement)
    {
        var version = (await RequireDetailsAsync(key)).Version;
        (await CallAsync<IBoardService, Result<CardView>>(s => s.MoveCardAsync(key, columnId, placement, version, null, Ct))).ValueOrThrow();
    }

    private Task<List<string>> RanksAsync() =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters().Where(w => w.Key.StartsWith("WEB-") && w.ParentId == null)
            .Select(w => w.Rank).ToListAsync(Ct));

    [Fact]
    public async Task A_new_card_goes_after_every_card_of_the_project_whatever_its_column()
    {
        await AddAsync(InProgress, "Build the header");   // WEB-1
        await AddAsync(Done, "Set up hosting");            // WEB-2
        await AddAsync(ToDo, "Pick the fonts");            // WEB-3

        Assert.Equal(["WEB-1", "WEB-2", "WEB-3"], await CardsByRankAsync());
    }

    [Fact]
    public async Task Moves_place_cards_between_neighbours_of_the_whole_project_and_never_repeat_a_key()
    {
        var a = await AddAsync(ToDo, "A");              // WEB-1
        var b = await AddAsync(InProgress, "B");        // WEB-2
        var c = await AddAsync(ToDo, "C");              // WEB-3
        var d = await AddAsync(InProgress, "D");        // WEB-4

        await MoveKeyAsync(c.Key, ToDo, new CardPlacement.Before(a.Key));
        Assert.Equal(["WEB-3", "WEB-1", "WEB-2", "WEB-4"], await CardsByRankAsync());

        await MoveKeyAsync(d.Key, InProgress, new CardPlacement.Before(b.Key));
        Assert.Equal(["WEB-3", "WEB-1", "WEB-4", "WEB-2"], await CardsByRankAsync());

        // "Top" of a column is just before that column's first card; "bottom" just after its last card.
        await MoveKeyAsync(b.Key, ToDo, CardPlacement.AtTop);
        Assert.Equal(["WEB-2", "WEB-3", "WEB-1", "WEB-4"], await CardsByRankAsync());

        await MoveKeyAsync(c.Key, InProgress, CardPlacement.AtEnd);
        Assert.Equal(["WEB-2", "WEB-1", "WEB-4", "WEB-3"], await CardsByRankAsync());

        var ranks = await RanksAsync();
        Assert.Equal(ranks.Count, ranks.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task The_rebalancer_renumbers_a_whole_Scrum_project_and_keeps_its_order()
    {
        var project = await QueryAsync(db => db.Projects.AsNoTracking().SingleAsync(p => p.Key == "WEB", Ct));
        var low = Rank.First();
        var high = Rank.After(low);
        var ranks = new List<string>();
        for (var i = 0; i < 300; i++)
        {
            high = Rank.Between(low, high);
            ranks.Add(high);
        }

        // Long keys spread over two columns: the project-wide order interleaves them.
        var longest = ranks.TakeLast(6).Reverse().ToList();
        var statuses = new[] { new StatusRef(ToDo, "To Do", StatusCategory.ToDo), new StatusRef(InProgress, "In Progress", StatusCategory.InProgress) };
        for (var i = 0; i < longest.Count; i++)
        {
            await Data.WorkItemAsync(project.Id, "WEB", statuses[i % 2], $"Item {i}", longest[i]);
        }

        var before = await CardsByRankAsync();

        Assert.True(await CallAsync<IRankRebalancer, int>(r => r.RebalanceLongRanksAsync(Ct)) > 0);

        Assert.Equal(before, await CardsByRankAsync());
        Assert.All(await RanksAsync(), r => Assert.False(Rank.NeedsRebalance(r)));
    }

    [Fact]
    public async Task A_card_restored_in_a_Scrum_project_goes_to_the_bottom()
    {
        await AddAsync(ToDo, "A");   // WEB-1
        await AddAsync(ToDo, "B");   // WEB-2
        (await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).ThrowIfFailed();
        await AddAsync(ToDo, "C");   // WEB-3

        ActAs(await Data.AdministratorAsync());
        (await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync("WEB-1", Ct))).ThrowIfFailed();

        Assert.Equal(["WEB-2", "WEB-3", "WEB-1"], await CardsByRankAsync());
    }
}

/// <summary>A Kanban project keeps an order per column, and switching it to Scrum gives it one order without history
/// (Phase 3 FR-005, research R8). WEB is amina's Kanban project.</summary>
public sealed class KanbanRankTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    [Fact]
    public async Task Each_column_of_a_Kanban_project_has_its_own_order()
    {
        await AddAsync(ToDo, "A");
        await AddAsync(InProgress, "B");

        var ranks = await QueryAsync(db => db.WorkItems.AsNoTracking().OrderBy(w => w.Id).Select(w => w.Rank).ToListAsync(Ct));

        Assert.Equal([Rank.First(), Rank.First()], ranks);
    }

    [Fact]
    public async Task Switching_to_Scrum_orders_deleted_cards_too_and_records_no_history()
    {
        await AddAsync(InProgress, "A");   // WEB-1
        await AddAsync(ToDo, "B");         // WEB-2
        await AddAsync(ToDo, "C");         // WEB-3
        (await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-3", Ct))).ThrowIfFailed();
        var history = await QueryAsync(db => db.WorkItemChanges.CountAsync(Ct));
        var version = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Value!.DetailsVersion;

        (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.SwitchTemplateAsync("WEB", ProjectTemplate.Scrum, version, Ct))).ValueOrThrow();

        Assert.Equal(["WEB-2", "WEB-3", "WEB-1"], await CardsByRankAsync());
        Assert.Equal(history, await QueryAsync(db => db.WorkItemChanges.CountAsync(Ct)));
    }
}
