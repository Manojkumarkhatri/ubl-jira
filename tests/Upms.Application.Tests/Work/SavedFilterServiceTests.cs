using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Work;

/// <summary>Saved filters (Phase 4 FR-039–FR-041, research R17): private by default, changed only by their owner, shared
/// with projects the owner belongs to, and shared only while the owner can still see those projects.</summary>
public sealed class SavedFilterServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _bilal = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        ActAs(Owner);
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Payments", "PAY", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
    }

    private Task<Result<SavedFilterView>> SaveAsync(string name, string query = "q=refund&assignee=none") =>
        CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.SaveAsync(name, query, Ct));

    private async Task<SavedFilterList> ListAsync() =>
        (await CallAsync<ISavedFilterService, Result<SavedFilterList>>(s => s.ListAsync(Ct))).ValueOrThrow();

    private Task<Result<SavedFilterView>> ShareAsync(long id, params string[] projects) =>
        CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.ShareAsync(id, projects, Ct));

    [Fact]
    public async Task P4_US3_AS8_Save_list_rename_update_and_delete()
    {
        var saved = (await SaveAsync("Unassigned refunds")).ValueOrThrow();
        Assert.Equal(("Unassigned refunds", "q=refund&assignee=none", "Amina Tester", true), (saved.Name, saved.Query, saved.OwnerName, saved.IsMine));
        Assert.Equal([saved.Id], (await ListAsync()).Mine.Select(f => f.Id));

        var renamed = (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.RenameAsync(saved.Id, "Open refunds", saved.Version, Ct))).ValueOrThrow();
        var updated = (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s =>
            s.UpdateAsync(saved.Id, "q=refund&category=ToDo", renamed.Version, Ct))).ValueOrThrow();
        Assert.Equal(("Open refunds", "q=refund&category=ToDo"), (updated.Name, updated.Query));

        Assert.True((await CallAsync<ISavedFilterService, Result>(s => s.DeleteAsync(saved.Id, Ct))).IsSuccess);
        Assert.Empty((await ListAsync()).Mine);
    }

    [Fact]
    public async Task A_second_filter_with_the_same_name_is_refused()
    {
        await SaveAsync("Unassigned refunds");

        var again = await SaveAsync("  UNASSIGNED refunds ");

        Assert.Equal((ErrorCodes.FilterNameTaken, "You already have a filter called Unassigned refunds."), (again.Error!.Code, again.Error.Message));
        ActAs(_bilal);
        Assert.True((await SaveAsync("Unassigned refunds")).IsSuccess); // names are unique per owner
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict()
    {
        var saved = (await SaveAsync("Refunds")).ValueOrThrow();
        await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.RenameAsync(saved.Id, "Open refunds", saved.Version, Ct));

        var stale = await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.UpdateAsync(saved.Id, "q=x", saved.Version, Ct));

        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
    }

    [Fact]
    public async Task P4_US3_AS10_A_filter_shared_with_a_project_can_be_run_but_not_changed_by_its_members()
    {
        var saved = (await SaveAsync("Open payment bugs")).ValueOrThrow();
        var shared = (await ShareAsync(saved.Id, "web")).ValueOrThrow();
        Assert.Equal(["WEB"], shared.SharedWith.Select(p => p.Key));

        ActAs(_bilal);
        var forBilal = Assert.Single((await ListAsync()).SharedWithMe);
        Assert.Equal((saved.Id, "Amina Tester", false), (forBilal.Id, forBilal.OwnerName, forBilal.IsMine));
        Assert.True((await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.GetAsync(saved.Id, Ct))).IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s =>
            s.RenameAsync(saved.Id, "Mine now", forBilal.Version, Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<ISavedFilterService, Result>(s => s.DeleteAsync(saved.Id, Ct))).Error!.Kind);

        ActAs(await Data.UserAsync("nora"));
        Assert.Empty((await ListAsync()).SharedWithMe);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.GetAsync(saved.Id, Ct))).Error!.Kind);
    }

    [Fact]
    public async Task P4_US3_AS10_A_filter_can_be_shared_only_with_projects_its_owner_belongs_to()
    {
        ActAs(_bilal);
        var saved = (await SaveAsync("Mine")).ValueOrThrow();

        Assert.Equal(ErrorCodes.NotAMember, (await ShareAsync(saved.Id, "PAY")).Error!.Code);
        Assert.Equal(ErrorCodes.NotAMember, (await ShareAsync(saved.Id, "NOPE")).Error!.Code);

        ActAs(await Data.AdministratorAsync());
        var adminFilter = (await SaveAsync("Everything")).ValueOrThrow();
        Assert.True((await ShareAsync(adminFilter.Id, "PAY", "WEB")).IsSuccess);
    }

    [Fact]
    public async Task P4_US3_AS11_Unsharing_leaving_the_project_or_deactivation_ends_the_sharing()
    {
        var omar = await MemberAsync("omar");
        ActAs(omar);
        var saved = (await SaveAsync("Omar's filter")).ValueOrThrow();
        await ShareAsync(saved.Id, "WEB");
        ActAs(_bilal);
        Assert.Single((await ListAsync()).SharedWithMe);

        ActAs(omar);
        await ShareAsync(saved.Id);
        ActAs(_bilal);
        Assert.Empty((await ListAsync()).SharedWithMe);

        ActAs(omar);
        await ShareAsync(saved.Id, "WEB");
        await Data.RemoveMemberAsync(await ProjectIdAsync("WEB"), omar.Id);
        ActAs(_bilal);
        Assert.Empty((await ListAsync()).SharedWithMe);

        await Data.MembersAsync("WEB", ProjectRole.Member, omar);
        Assert.Single((await ListAsync()).SharedWithMe);
        await DeactivateAsync(omar);
        Assert.Empty((await ListAsync()).SharedWithMe);
    }

    private Task<long> ProjectIdAsync(string key) =>
        QueryAsync(db => Task.FromResult(db.Projects.Single(p => p.Key == key).Id));

    private async Task DeactivateAsync(User user)
    {
        await using var scope = Harness.CreateScope();
        var db = (Upms.Application.Common.IAppDbContext)scope.ServiceProvider.GetService(typeof(Upms.Application.Common.IAppDbContext))!;
        db.Users.Single(u => u.Id == user.Id).IsActive = false;
        await db.SaveChangesAsync(Ct);
    }
}
