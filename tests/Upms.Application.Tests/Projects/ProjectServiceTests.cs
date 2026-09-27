using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>Creating, listing and editing projects (FR-011 to FR-014; Phase 2 FR-002, FR-006, FR-007, FR-015).</summary>
public sealed class ProjectServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _amina = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _amina = await Data.UserAsync("amina");
        ActAs(_amina);
    }

    private Task<Result<string>> CreateAsync(string name, string key, string? description = null) =>
        CallAsync<IProjectService, Result<string>>(s => s.CreateAsync(name, key, description, Ct));

    [Fact]
    public async Task US1_AS3_Creating_a_project_opens_a_board_with_three_columns_and_the_creator_as_owner()
    {
        var created = await CreateAsync("Website Revamp", "WEB", "The new public site");

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal("WEB", created.Value);
        var details = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Value!;
        Assert.Equal("Website Revamp", details.Name);
        Assert.Equal("The new public site", details.Description);
        Assert.Equal(_amina.DisplayName, details.OwnerDisplayName);
        Assert.True(details.CanManage);

        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).Value!;
        Assert.Equal(["To Do", "In Progress", "Done"], board.Columns.Select(c => c.Name));
        Assert.Equal([StatusCategory.ToDo, StatusCategory.InProgress, StatusCategory.Done], board.Columns.Select(c => c.Category));
    }

    [Fact]
    public async Task P2_US1_AS1_The_creator_becomes_the_only_member_as_Project_Admin()
    {
        await CreateAsync("Website Revamp", "WEB");

        var team = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct))).Value!;

        var member = Assert.Single(team.Members);
        Assert.Equal(_amina.Id, member.UserId);
        Assert.Equal(ProjectRole.ProjectAdmin, member.Role);
    }

    [Fact]
    public async Task P2_US1_AS4_Non_members_are_told_the_project_does_not_exist()
    {
        await CreateAsync("Website Revamp", "WEB");

        ActAs(await Data.UserAsync("carla"));

        Assert.Equal(ErrorKind.NotFound, (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Error!.Kind);
        Assert.Empty((await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s => s.ListAsync(PageRequest.First, Ct))).Value!.Items);
    }

    [Fact]
    public async Task US1_AS3_A_key_is_suggested_and_avoids_keys_in_use()
    {
        Assert.Equal("WR", await CallAsync<IProjectService, string>(s => s.SuggestKeyAsync("Website Revamp", Ct)));

        await CreateAsync("Warehouse Relocation", "WR");

        Assert.Equal("WR2", await CallAsync<IProjectService, string>(s => s.SuggestKeyAsync("Website Revamp", Ct)));
    }

    [Fact]
    public async Task US1_AS4_A_key_or_name_already_in_use_is_refused()
    {
        await CreateAsync("Website Revamp", "WEB");

        Assert.Equal(ErrorCodes.DuplicateProjectKey, (await CreateAsync("Web Shop", "WEB")).Error!.Code);
        Assert.Equal(ErrorCodes.DuplicateProjectName, (await CreateAsync("website revamp", "WR")).Error!.Code);
    }

    [Fact]
    public async Task An_invalid_key_is_refused()
    {
        var result = await CreateAsync("Website Revamp", "2WEB");

        Assert.Equal(ErrorCodes.InvalidProjectKey, result.Error!.Code);
        Assert.Contains("Key", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task P2_FR015_The_list_shows_only_the_callers_projects_with_their_role_and_open_tasks_sorted_and_paged()
    {
        var bilal = await Data.UserAsync("bilal");
        await CreateAsync("Website Revamp", "WEB");
        await CreateAsync("Human Resources", "HR");   // bilal is not a member
        await Data.MembersAsync("WEB", ProjectRole.Member, bilal);
        ActAs(bilal);
        await CreateAsync("Accounting Close", "ACC");
        await CreateAsync("Mobile App", "MOB");
        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).Value!;
        var toDo = board.Columns[0].Id;
        var done = board.Columns[2].Id;
        await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", toDo, "Open one", Ct));
        await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", toDo, "Open two", Ct));
        await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", done, "Finished", Ct));

        var first = (await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s => s.ListAsync(new PageRequest(1, 2), Ct))).Value!;
        var second = (await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s => s.ListAsync(new PageRequest(2, 2), Ct))).Value!;

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(["Accounting Close", "Mobile App"], first.Items.Select(p => p.Name));
        Assert.All(first.Items, p => Assert.Equal(ProjectRole.ProjectAdmin, p.MyRole));
        var web = Assert.Single(second.Items);
        Assert.Equal(("WEB", "Website Revamp", ProjectRole.Member, false, 2), (web.Key, web.Name, web.MyRole, web.AdministratorAccess, web.OpenItemCount));
    }

    [Fact]
    public async Task P2_US1_AS8_Administrators_see_every_project_marked_where_they_are_not_members()
    {
        await CreateAsync("Website Revamp", "WEB");
        ActAs(await Data.AdministratorAsync());
        await CreateAsync("Admin Tools", "ADM");

        var list = (await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s => s.ListAsync(PageRequest.First, Ct))).Value!;

        Assert.Equal(["Admin Tools", "Website Revamp"], list.Items.Select(p => p.Name));
        Assert.Equal((ProjectRole.ProjectAdmin, false), (list.Items[0].MyRole, list.Items[0].AdministratorAccess));
        Assert.Equal(((ProjectRole?)null, true), (list.Items[1].MyRole, list.Items[1].AdministratorAccess));
    }

    [Fact]
    public async Task Details_can_be_edited_by_Project_Admins_and_administrators_only()
    {
        await CreateAsync("Website Revamp", "WEB");
        var version = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Value!.DetailsVersion;

        var bilal = await Data.UserAsync("bilal");
        await Data.MembersAsync("WEB", ProjectRole.Member, bilal);
        ActAs(bilal);
        var forbidden = await CallAsync<IProjectService, Result<ProjectDetails>>(s =>
            s.UpdateDetailsAsync("WEB", "Hijacked", null, version, Ct));
        Assert.Equal(ErrorKind.Forbidden, forbidden.Error!.Kind);
        Assert.False((await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Value!.CanManage);

        ActAs(await Data.AdministratorAsync());
        var byAdmin = await CallAsync<IProjectService, Result<ProjectDetails>>(s =>
            s.UpdateDetailsAsync("WEB", "Website 2.0", "Second phase", version, Ct));
        Assert.True(byAdmin.IsSuccess, byAdmin.Error?.Message);
        Assert.Equal("Website 2.0", byAdmin.Value!.Name);

        ActAs(_amina);
        var stale = await CallAsync<IProjectService, Result<ProjectDetails>>(s =>
            s.UpdateDetailsAsync("WEB", "Website Three", null, version, Ct));
        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal("Website 2.0", Assert.IsType<ProjectDetails>(stale.Error.Current).Name);
    }

    [Fact]
    public async Task Renaming_to_a_name_in_use_is_refused()
    {
        await CreateAsync("Website Revamp", "WEB");
        await CreateAsync("Mobile App", "MOB");
        var version = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("MOB", Ct))).Value!.DetailsVersion;

        var result = await CallAsync<IProjectService, Result<ProjectDetails>>(s =>
            s.UpdateDetailsAsync("MOB", "WEBSITE REVAMP", null, version, Ct));

        Assert.Equal(ErrorCodes.DuplicateProjectName, result.Error!.Code);
    }

    [Fact]
    public async Task Anonymous_callers_cannot_list_or_create()
    {
        ActAs(null);

        Assert.Equal(ErrorKind.Forbidden, (await CreateAsync("Website Revamp", "WEB")).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s =>
            s.ListAsync(PageRequest.First, Ct))).Error!.Kind);
    }
}
