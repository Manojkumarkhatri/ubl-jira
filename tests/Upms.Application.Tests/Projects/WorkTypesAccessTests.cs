using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Work;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Projects;

/// <summary>Every space carries its work types, and every create path follows them (Jira parity pass FR-003, FR-017;
/// research R14). WEB is amina's Kanban space.</summary>
public sealed class WorkTypesAccessTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    private Task<int> TurnOnOnlyAsync(WorkTypeSet set) =>
        QueryAsync(db => db.Database.ExecuteSqlAsync($"UPDATE Projects SET WorkTypes = {(int)set} WHERE [Key] = 'WEB'", Ct));

    [Fact]
    public async Task A_new_space_has_all_four_work_types()
    {
        Assert.Equal(WorkTypeSet.All, await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.WorkTypes).SingleAsync(Ct)));
    }

    [Fact]
    public async Task The_access_record_carries_the_spaces_work_types()
    {
        await TurnOnOnlyAsync(WorkTypeSet.Epic | WorkTypeSet.Task);

        var access = await CallAsync<IProjectAccess, Result<ProjectAccessInfo>>(a => a.RequireAsync("WEB", ProjectRight.View, Ct));

        Assert.Equal(WorkTypeSet.Epic | WorkTypeSet.Task, access.ValueOrThrow().WorkTypes);
    }

    [Fact]
    public async Task JP_EdgeCase_Creating_a_type_the_space_turned_off_is_refused()
    {
        await TurnOnOnlyAsync(WorkTypeSet.Epic | WorkTypeSet.Task);

        var created = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Bug, "Broken export"), Ct));
        var inline = await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", ToDo, "Broken export", WorkItemType.Bug, Ct));

        foreach (var refused in new[] { created.Error, inline.Error })
        {
            Assert.Equal(ErrorCodes.TypeNotAllowed, refused?.Code);
            Assert.Equal("This space no longer uses Bugs.", refused?.Message);
        }

        Assert.Equal(0, await QueryAsync(db => db.WorkItems.CountAsync(Ct)));
    }

    [Fact]
    public async Task Types_that_are_on_and_sub_tasks_are_still_created()
    {
        await TurnOnOnlyAsync(WorkTypeSet.Task);

        var task = await AddAsync(ToDo, "Export the ledger");
        await AddSubtaskAsync(task.Key, "Write the query");

        Assert.Equal(2, await QueryAsync(db => db.WorkItems.CountAsync(Ct)));
    }

    private async Task AddSubtaskAsync(string parentKey, string title) =>
        (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync(parentKey, title, Ct))).ValueOrThrow();
}
