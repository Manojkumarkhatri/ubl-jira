using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Work;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Projects;

/// <summary>Space admins turn work types on and off (Jira parity pass US3 scenario 6, FR-017). WEB is amina's Kanban space.</summary>
public sealed class WorkTypesTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    private Task<Result<ProjectDetails>> DetailsAsync() => CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct));

    private async Task<Result<ProjectDetails>> SetAsync(WorkTypeSet types, int? version = null)
    {
        version ??= (await DetailsAsync()).ValueOrThrow().DetailsVersion;
        return await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.SetWorkTypesAsync("WEB", types, version.Value, Ct));
    }

    [Fact]
    public async Task JP_US3_AS6_A_space_admin_turns_types_off_and_on_and_it_is_audited()
    {
        var saved = (await SetAsync(WorkTypeSet.Story | WorkTypeSet.Task)).ValueOrThrow();

        Assert.Equal(WorkTypeSet.Story | WorkTypeSet.Task, saved.WorkTypes);
        var audit = (await AuditEventsAsync()).Single(e => e.EventType == AuditEventType.ProjectWorkTypesChanged);
        Assert.Equal("WEB", audit.Target);
        Assert.Contains("Epic", audit.Details, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.TypeNotAllowed, (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Bug, "Broken export"), Ct))).Error!.Code);
        Assert.DoesNotContain(WorkItemType.Bug, (await CallAsync<ICreateWorkService, Result<CreateOptions>>(s => s.OptionsAsync("WEB", Ct))).ValueOrThrow().Types);

        (await SetAsync(WorkTypeSet.All)).ValueOrThrow();
        Assert.Contains(WorkItemType.Bug, (await CallAsync<ICreateWorkService, Result<CreateOptions>>(s => s.OptionsAsync("WEB", Ct))).ValueOrThrow().Types);
    }

    [Fact]
    public async Task JP_US3_AS6_A_type_that_items_use_stays_on_deleted_items_included()
    {
        var bug = await AddAsync(ToDo, "Broken export", WorkItemType.Bug);
        await AddAsync(ToDo, "Another bug", WorkItemType.Bug);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(bug.Key, Ct))).IsSuccess);

        var refused = await SetAsync(WorkTypeSet.Story | WorkTypeSet.Task | WorkTypeSet.Epic);

        Assert.Equal(ErrorCodes.WorkTypeInUse, refused.Error!.Code);
        Assert.Equal("Bugs are still used by 2 items in this space.", refused.Error.Message);
        var details = (await DetailsAsync()).ValueOrThrow();
        Assert.Equal(2, details.TypeCounts![WorkTypeSet.Bug]);
        Assert.Equal(WorkTypeSet.All, details.WorkTypes);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_members_are_refused()
    {
        var stale = (await DetailsAsync()).ValueOrThrow().DetailsVersion;
        (await SetAsync(WorkTypeSet.Story | WorkTypeSet.Task)).ValueOrThrow();

        Assert.Equal(ErrorKind.Conflict, (await SetAsync(WorkTypeSet.Task, stale)).Error!.Kind);

        var omar = await MemberAsync("omar");
        ActAs(omar);
        Assert.Equal(ErrorKind.Forbidden, (await SetAsync(WorkTypeSet.Task)).Error!.Kind);
    }

    [Fact]
    public async Task Stage_gate_spaces_keep_their_types()
    {
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Core Banking", "CBU", null, ProjectTemplate.StageGate, Ct))).ValueOrThrow();
        var version = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("CBU", Ct))).ValueOrThrow().DetailsVersion;

        var refused = await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.SetWorkTypesAsync("CBU", WorkTypeSet.Task, version, Ct));

        Assert.Equal(ErrorCodes.TemplateSwitchNotAllowed, refused.Error!.Code);
        Assert.Equal(0, await QueryAsync(db => db.AuditEvents.CountAsync(e => e.EventType == AuditEventType.ProjectWorkTypesChanged, Ct)));
    }
}
