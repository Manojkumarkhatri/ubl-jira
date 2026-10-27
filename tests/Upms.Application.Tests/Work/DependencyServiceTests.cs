using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Dependencies between items of one project: adding, refusing, late marks and removal (Phase 3 FR-047–FR-052).
/// WEB-1 "Build the API" and WEB-2 "Build the app" are amina's tasks.</summary>
public sealed class DependencyServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static readonly DateOnly Nov10 = new(2026, 11, 10);
    private static readonly DateOnly Nov12 = new(2026, 11, 12);

    private string _api = "";
    private string _app = "";

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _api = (await AddAsync(ToDo, "Build the API")).Key;
        _app = (await AddAsync(ToDo, "Build the app")).Key;
    }

    private Task<Result<DependencyList>> TryAddAsync(string waiting, string waitedFor) =>
        CallAsync<IDependencyService, Result<DependencyList>>(s => s.AddAsync(waiting, waitedFor, Ct));

    private Task<Result<DependencyList>> TryRemoveAsync(string waiting, string waitedFor) =>
        CallAsync<IDependencyService, Result<DependencyList>>(s => s.RemoveAsync(waiting, waitedFor, Ct));

    private async Task<DependencyList> ListOfAsync(string key) => (await RequireDetailsAsync(key)).Dependencies!;

    [Fact]
    public async Task P3_US4_AS5_A_dependency_shows_in_both_drawers_and_both_histories()
    {
        var list = (await TryAddAsync(_app, _api)).ValueOrThrow();

        Assert.Equal([_api], list.WaitsFor.Select(d => d.Key));
        Assert.Empty(list.Blocks);
        Assert.Equal([_app], (await ListOfAsync(_api)).Blocks.Select(d => d.Key));
        Assert.Contains((await HistoryAsync(_app)).Items, h => h.Field == WorkItemField.DependencyAdded && h.NewValue == $"waits for {_api} Build the API");
        Assert.Contains((await HistoryAsync(_api)).Items, h => h.Field == WorkItemField.DependencyAdded && h.NewValue == $"is waited for by {_app} Build the app");
    }

    [Fact]
    public async Task P3_US4_AS6_Late_marks_follow_dates_and_status_and_no_date_moves()
    {
        (await EditAsync(_app, new WorkItemEdit.Dates(Nov10, new DateOnly(2026, 11, 20)))).ValueOrThrow();
        (await EditAsync(_api, new WorkItemEdit.Dates(null, Nov10))).ValueOrThrow();
        (await TryAddAsync(_app, _api)).ValueOrThrow();
        Assert.False((await ListOfAsync(_app)).WaitsFor.Single().IsLate);

        (await EditAsync(_api, new WorkItemEdit.Dates(null, Nov12))).ValueOrThrow();
        Assert.True((await ListOfAsync(_app)).WaitsFor.Single().IsLate);
        Assert.True((await ListOfAsync(_api)).Blocks.Single().IsLate);
        Assert.Equal((Nov10, new DateOnly(2026, 11, 20)), ((await RequireDetailsAsync(_app)).StartDate, (await RequireDetailsAsync(_app)).DueDate));

        (await EditAsync(_api, new WorkItemEdit.Status(Done))).ValueOrThrow();
        Assert.False((await ListOfAsync(_app)).WaitsFor.Single().IsLate);
    }

    [Fact]
    public async Task P3_US4_AS7_A_loop_is_refused_directly_and_through_other_items()
    {
        var web = (await AddAsync(ToDo, "Launch")).Key;
        (await TryAddAsync(_app, _api)).ValueOrThrow();
        (await TryAddAsync(web, _app)).ValueOrThrow();

        Assert.Equal(ErrorCodes.DependencyLoop, (await TryAddAsync(_api, _app)).Error!.Code);
        Assert.Equal(ErrorCodes.DependencyLoop, (await TryAddAsync(_api, web)).Error!.Code);
    }

    [Fact]
    public async Task P3_US4_AS7_Two_opposite_links_added_at_the_same_moment_leave_one()
    {
        async Task<Result<DependencyList>> AddInFlow(string waiting, string waitedFor)
        {
            await Task.Yield();
            Harness.CurrentUser.UseInThisFlow(Owner.Id);
            return await TryAddAsync(waiting, waitedFor);
        }

        var results = await Task.WhenAll(AddInFlow(_app, _api), AddInFlow(_api, _app));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(ErrorCodes.DependencyLoop, Assert.Single(results, r => !r.IsSuccess).Error!.Code);
    }

    [Fact]
    public async Task Links_to_itself_repeats_and_its_own_sub_tasks_are_refused()
    {
        var subtask = (await AddSubtaskAsync(_api, "Write the endpoints")).Subtasks.Items[0].Key;
        (await TryAddAsync(_app, _api)).ValueOrThrow();

        Assert.Equal(ErrorCodes.DependencyToSelf, (await TryAddAsync(_api, _api)).Error!.Code);
        Assert.Equal(ErrorCodes.DuplicateDependency, (await TryAddAsync(_app, _api)).Error!.Code);
        Assert.Equal(ErrorCodes.DependencyInHierarchy, (await TryAddAsync(subtask, _api)).Error!.Code);
    }

    [Fact]
    public async Task Items_of_another_project_cannot_be_linked()
    {
        await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Intranet", "INT", null, ProjectTemplate.Kanban, Ct));
        var other = (await CreateAsync(WorkItemType.Task, "Other work", projectKey: "INT")).Key;

        Assert.Equal(ErrorKind.NotFound, (await TryAddAsync(_app, other)).Error!.Kind);
    }

    [Fact]
    public async Task P3_US4_AS9_Viewers_cannot_add_or_remove_dependencies()
    {
        (await TryAddAsync(_app, _api)).ValueOrThrow();
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        Assert.Equal(ErrorKind.Forbidden, (await TryAddAsync(_api, _app)).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await TryRemoveAsync(_app, _api)).Error!.Kind);
        Assert.Single((await ListOfAsync(_app)).WaitsFor);
    }

    [Fact]
    public async Task Removing_a_dependency_is_recorded_on_both_items()
    {
        (await TryAddAsync(_app, _api)).ValueOrThrow();

        var list = (await TryRemoveAsync(_app, _api)).ValueOrThrow();

        Assert.Empty(list.WaitsFor);
        Assert.Contains((await HistoryAsync(_app)).Items, h => h.Field == WorkItemField.DependencyRemoved && h.OldValue == $"waits for {_api} Build the API");
        Assert.Contains((await HistoryAsync(_api)).Items, h => h.Field == WorkItemField.DependencyRemoved);
    }

    [Fact]
    public async Task P3_US4_AS10_Deleting_an_item_removes_its_links_and_restoring_does_not_bring_them_back()
    {
        var launch = (await AddAsync(ToDo, "Launch")).Key;
        var subtask = (await AddSubtaskAsync(_api, "Write the endpoints")).Subtasks.Items[0].Key;
        (await TryAddAsync(_app, _api)).ValueOrThrow();
        (await TryAddAsync(launch, subtask)).ValueOrThrow();

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(_api, Ct))).IsSuccess);

        Assert.Empty((await ListOfAsync(_app)).WaitsFor);
        Assert.Empty((await ListOfAsync(launch)).WaitsFor);
        Assert.Contains((await HistoryAsync(_app)).Items, h => h.Field == WorkItemField.DependencyRemoved);
        Assert.Contains((await HistoryAsync(launch)).Items, h => h.Field == WorkItemField.DependencyRemoved);
        Assert.Equal(0, await QueryAsync(db => db.Dependencies.CountAsync(Ct)));

        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(_api, Ct))).IsSuccess);
        ActAs(Owner);
        Assert.Empty((await ListOfAsync(_api)).Blocks);
    }

    [Fact]
    public async Task Items_are_found_by_key_or_words_in_the_title()
    {
        var byTitle = (await CallAsync<IDependencyService, Result<IReadOnlyList<ItemOption>>>(s => s.FindItemsAsync("WEB", "app", Ct))).ValueOrThrow();
        var byKey = (await CallAsync<IDependencyService, Result<IReadOnlyList<ItemOption>>>(s => s.FindItemsAsync("WEB", _api.ToLowerInvariant(), Ct))).ValueOrThrow();

        Assert.Equal([_app], byTitle.Select(i => i.Key));
        Assert.Equal([_api], byKey.Select(i => i.Key));
    }
}
