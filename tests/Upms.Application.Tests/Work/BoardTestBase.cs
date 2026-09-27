using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Work;

/// <summary>A signed-in owner with project WEB and helpers for board calls.</summary>
public abstract class BoardTestBase(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    protected User Owner { get; private set; } = null!;

    protected BoardView Board { get; private set; } = null!;

    protected long ToDo => Board.Columns[0].Id;

    protected long InProgress => Board.Columns[1].Id;

    protected long Done => Board.Columns[2].Id;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Owner = await Data.UserAsync("amina");
        ActAs(Owner);
        var created = await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Website Revamp", "WEB", null, Ct));
        Assert.True(created.IsSuccess, created.Error?.Message);
        Board = await BoardAsync();
    }

    protected async Task<BoardView> BoardAsync(bool showAllDone = false) =>
        (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", showAllDone, Ct))).ValueOrThrow();

    protected async Task<CardView> AddAsync(long columnId, string title) =>
        (await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", columnId, title, Ct))).ValueOrThrow();

    protected Task<Result<CardView>> MoveAsync(CardView card, long columnId, CardPlacement placement) =>
        CallAsync<IBoardService, Result<CardView>>(s => s.MoveCardAsync(card.Key, columnId, placement, card.Version, Ct));

    protected async Task<IReadOnlyList<string>> KeysInAsync(long columnId, bool showAllDone = false) =>
        (await BoardAsync(showAllDone)).Columns.Single(c => c.Id == columnId).Cards.Select(c => c.Key).ToList();
}
