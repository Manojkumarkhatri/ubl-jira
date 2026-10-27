using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>A signed-in owner with project WEB (Kanban unless a test class chooses another template) and helpers for board
/// calls.</summary>
public abstract class BoardTestBase(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    /// <summary>WEB's template (Phase 3 FR-001).</summary>
    protected virtual ProjectTemplate Template => ProjectTemplate.Kanban;

    protected User Owner { get; private set; } = null!;

    protected BoardView Board { get; private set; } = null!;

    protected long ToDo => Board.Columns[0].Id;

    protected long InProgress => Board.Columns[1].Id;

    protected long Done => Board.Columns.First(c => c.Category == StatusCategory.Done).Id;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Owner = await Data.UserAsync("amina");
        ActAs(Owner);
        var created = await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Website Revamp", "WEB", null, Template, Ct));
        Assert.True(created.IsSuccess, created.Error?.Message);
        Board = await BoardAsync();
    }

    /// <summary>A new user on WEB's team, since projects are members-only (Phase 2 FR-002).</summary>
    protected async Task<User> MemberAsync(string userName, ProjectRole role = ProjectRole.Member)
    {
        var user = await Data.UserAsync(userName);
        await Data.MembersAsync("WEB", role, user);
        return user;
    }

    protected async Task<BoardView> BoardAsync(bool showAllDone = false) =>
        (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", showAllDone, Ct))).ValueOrThrow();

    protected async Task<CardView> AddAsync(long columnId, string title, WorkItemType type = WorkItemType.Task) =>
        (await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", columnId, title, type, Ct))).ValueOrThrow();

    /// <summary>A work item of any type through the list's "What needs to be done?" (Phase 3 FR-010).</summary>
    protected async Task<WorkItemDetails> CreateAsync(WorkItemType type, string title, string? containerKey = null, string projectKey = "WEB") =>
        (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.CreateAsync(projectKey, new NewWorkItem(type, title, containerKey), Ct)))
        .ValueOrThrow();

    /// <summary>WEB's cards in rank order, deleted ones included (Phase 3 research R8).</summary>
    protected Task<List<string>> CardsByRankAsync() =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.Key.StartsWith("WEB-") && w.ParentId == null)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => w.Key)
            .ToListAsync(Ct));

    protected Task<Result<CardView>> MoveAsync(CardView card, long columnId, CardPlacement placement) =>
        CallAsync<IBoardService, Result<CardView>>(s => s.MoveCardAsync(card.Key, columnId, placement, card.Version, null, Ct));

    protected async Task<IReadOnlyList<string>> KeysInAsync(long columnId, bool showAllDone = false) =>
        (await BoardAsync(showAllDone)).Columns.Single(c => c.Id == columnId).Cards.Select(c => c.Key).ToList();
}
