using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>WEB as amina's Scrum project, with backlog and sprint helpers (Phase 3 User Story 2).</summary>
public abstract class ScrumTestBase(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected static readonly DateOnly October5 = new(2026, 10, 5);

    protected override ProjectTemplate Template => ProjectTemplate.Scrum;

    protected Task<Result<BacklogView>> TryBacklogAsync(int page = 1, string projectKey = "WEB") =>
        CallAsync<IBacklogService, Result<BacklogView>>(s => s.GetAsync(projectKey, new PageRequest(page), Ct));

    protected async Task<BacklogView> BacklogAsync(int page = 1) => (await TryBacklogAsync(page)).ValueOrThrow();

    protected async Task<List<string>> BacklogKeysAsync() => KeysOf((await BacklogAsync()).Backlog.Items);

    protected static List<string> KeysOf(IEnumerable<BacklogRow> rows) => rows.Select(r => r.Key).ToList();

    /// <summary>A story (or another card type) through the list's create box, which puts it at the bottom of the backlog,
    /// with an estimate if given.</summary>
    protected async Task<string> StoryAsync(string title, decimal? estimate = null, WorkItemType type = WorkItemType.Story)
    {
        var key = (await CreateAsync(type, title)).Key;
        if (estimate is { } points)
        {
            (await EditAsync(key, new WorkItemEdit.Estimate(points))).ValueOrThrow();
        }

        return key;
    }

    protected async Task<Result<BacklogRow>> TryMoveAsync(string key, BacklogTarget target, BacklogPlacement? placement = null,
        byte[]? version = null)
    {
        version ??= (await RequireDetailsAsync(key)).Version;
        return await CallAsync<IBacklogService, Result<BacklogRow>>(s =>
            s.MoveAsync(key, target, placement ?? BacklogPlacement.KeepRank, version, Ct));
    }

    protected async Task<BacklogRow> MoveAsync(string key, BacklogTarget target, BacklogPlacement? placement = null) =>
        (await TryMoveAsync(key, target, placement)).ValueOrThrow();

    protected static BacklogTarget Into(SprintSection sprint) => new BacklogTarget.Sprint(sprint.Id);

    protected Task<Result<SprintSection>> TryCreateSprintAsync(string projectKey = "WEB") =>
        CallAsync<ISprintService, Result<SprintSection>>(s => s.CreateAsync(projectKey, Ct));

    protected async Task<SprintSection> NewSprintAsync() => (await TryCreateSprintAsync()).ValueOrThrow();

    /// <summary>The sprint as the backlog shows it now, with its current version.</summary>
    protected async Task<SprintSection> SprintAsync(long id)
    {
        var view = await BacklogAsync();
        return new[] { view.Active }.OfType<SprintSection>().Concat(view.Planned).Single(s => s.Id == id);
    }

    protected async Task<Result<SprintSection>> TryStartAsync(long id, DateOnly? start = null, DateOnly? end = null, string? goal = null,
        byte[]? version = null)
    {
        version ??= (await SprintAsync(id)).Version;
        return await CallAsync<ISprintService, Result<SprintSection>>(s => s.StartAsync(id, start ?? October5, end, goal, version, Ct));
    }

    protected async Task<SprintSection> StartAsync(long id, DateOnly? start = null, DateOnly? end = null, string? goal = null) =>
        (await TryStartAsync(id, start, end, goal)).ValueOrThrow();

    protected async Task<Result<SprintSection>> TryCompleteAsync(long id, long? moveOpenItemsTo = null, byte[]? version = null)
    {
        version ??= (await SprintAsync(id)).Version;
        return await CallAsync<ISprintService, Result<SprintSection>>(s => s.CompleteAsync(id, moveOpenItemsTo, version, Ct));
    }

    protected async Task<SprintSection> CompleteAsync(long id, long? moveOpenItemsTo = null) =>
        (await TryCompleteAsync(id, moveOpenItemsTo)).ValueOrThrow();

    protected async Task MarkDoneAsync(string key) => (await EditAsync(key, new WorkItemEdit.Status(Done))).ValueOrThrow();

    protected async Task ReopenAsync(string key) => (await EditAsync(key, new WorkItemEdit.Status(ToDo))).ValueOrThrow();

    protected Task<Result<SprintReport>> TryReportAsync(int number) =>
        CallAsync<ISprintService, Result<SprintReport>>(s => s.GetReportAsync("WEB", number, Ct));

    /// <summary>The sprint's scope log as (key, kind, estimate), oldest first.</summary>
    protected Task<List<(string Key, SprintScopeKind Kind, decimal? Estimate)>> ScopeEventsAsync(long sprintId) =>
        QueryAsync(async db => (await (
                from e in db.SprintScopeEvents.AsNoTracking()
                join w in db.WorkItems.IgnoreQueryFilters() on e.WorkItemId equals w.Id
                where e.SprintId == sprintId
                orderby e.Id
                select new { w.Key, e.Kind, e.Estimate })
            .ToListAsync(Ct))
            .ConvertAll(e => (e.Key, e.Kind, e.Estimate)));

    protected Task<long?> SprintIdOfAsync(string key) =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters().Where(w => w.Key == key).Select(w => w.SprintId).SingleAsync(Ct));
}
