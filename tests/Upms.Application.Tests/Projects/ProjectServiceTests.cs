using Microsoft.EntityFrameworkCore;
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

    private Task<Result<string>> CreateAsync(string name, string key, string? description = null,
        ProjectTemplate template = ProjectTemplate.Kanban) =>
        CallAsync<IProjectService, Result<string>>(s => s.CreateAsync(name, key, description, template, Ct));

    private Task<Result<ProjectDetails>> DetailsAsync(string key) =>
        CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync(key, Ct));

    private Task<Result<ProjectDetails>> SwitchAsync(string key, ProjectTemplate target, int? version = null) =>
        CallAsync<IProjectService, Result<ProjectDetails>>(async s =>
            await s.SwitchTemplateAsync(key, target, version ?? (await s.GetAsync(key, Ct)).Value!.DetailsVersion, Ct));

    private Task<BoardView> BoardOfAsync(string key) =>
        CallAsync<IBoardService, BoardView>(async s => (await s.GetAsync(key, false, Ct)).Value!);

    private Task<string> AddCardAsync(string key, long columnId, string title) =>
        CallAsync<IBoardService, string>(async s => (await s.CreateInlineAsync(key, columnId, title, Domain.Work.WorkItemType.Task, Ct)).Value!.Key);

    /// <summary>The project's cards in rank order, deleted ones included (Phase 3 research R8).</summary>
    private Task<List<string>> CardsByRankAsync(string key) =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ParentId == null && db.Projects.Any(p => p.Id == w.ProjectId && p.Key == key))
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => w.Key)
            .ToListAsync(Ct));

    /// <summary>Each column's cards in rank order, as a board would show them.</summary>
    private async Task<List<(string Name, string Keys)>> ColumnsByRankAsync(string key)
    {
        var columns = await QueryAsync(db => db.ProjectStatuses.AsNoTracking()
            .Where(s => db.Projects.Any(p => p.Id == s.ProjectId && p.Key == key))
            .OrderBy(s => s.Position)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(Ct));
        var cards = await QueryAsync(db => db.WorkItems.AsNoTracking()
            .Where(w => w.ParentId == null && db.Projects.Any(p => p.Id == w.ProjectId && p.Key == key))
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => new { w.Key, w.StatusId })
            .ToListAsync(Ct));
        return columns.ConvertAll(c => (c.Name, string.Join(",", cards.Where(w => w.StatusId == c.Id).Select(w => w.Key))));
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban, new[] { "To Do", "In Progress", "Done" })]
    [InlineData(ProjectTemplate.Scrum, new[] { "To Do", "In Progress", "In Review", "Done" })]
    [InlineData(ProjectTemplate.StageGate, new[] { "Not Started", "In Progress", "On Hold", "Completed" })]
    public async Task P3_US1_AS1_AS2_A_project_starts_with_its_templates_columns(ProjectTemplate template, string[] columns)
    {
        Assert.True((await CreateAsync("Mobile App", "MOB", template: template)).IsSuccess);

        Assert.Equal(columns, (await BoardOfAsync("MOB")).Columns.Select(c => c.Name));
        var details = (await DetailsAsync("MOB")).Value!;
        Assert.Equal(template, details.Template);
        var summary = Assert.Single((await CallAsync<IProjectService, Result<Page<ProjectSummary>>>(s => s.ListAsync(PageRequest.First, Ct))).Value!.Items);
        Assert.Equal(template, summary.Template);
    }

    [Fact]
    public async Task P3_US1_AS2_A_Stage_gate_project_starts_with_four_stages_in_order()
    {
        await CreateAsync("Core Banking Upgrade", "CBU", template: ProjectTemplate.StageGate);

        var stages = await QueryAsync(db => db.WorkItems.AsNoTracking()
            .Where(w => w.Key.StartsWith("CBU-"))
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Select(w => new { w.Key, w.Type, w.Title, w.StatusId })
            .ToListAsync(Ct));
        var notStarted = (await BoardOfAsync("CBU")).Columns[0].Id;

        Assert.Equal(["CBU-1", "CBU-2", "CBU-3", "CBU-4"], stages.Select(s => s.Key));
        Assert.Equal(["Initiation", "Planning", "Execution", "Closure"], stages.Select(s => s.Title));
        Assert.All(stages, s => Assert.Equal((Domain.Work.WorkItemType.Stage, notStarted), (s.Type, s.StatusId)));
        Assert.All((await BoardOfAsync("CBU")).Columns, c => Assert.Empty(c.Cards));
    }

    [Fact]
    public async Task P3_US1_AS9_Switching_to_Scrum_orders_the_cards_column_by_column_and_is_audited()
    {
        await CreateAsync("Website Revamp", "WEB");
        var board = await BoardOfAsync("WEB");
        var (toDo, inProgress, done) = (board.Columns[0].Id, board.Columns[1].Id, board.Columns[2].Id);
        await AddCardAsync("WEB", inProgress, "Build the header");    // WEB-1
        await AddCardAsync("WEB", toDo, "Pick the fonts");            // WEB-2
        await AddCardAsync("WEB", done, "Set up hosting");            // WEB-3
        await AddCardAsync("WEB", toDo, "Write the copy");            // WEB-4
        await AddCardAsync("WEB", inProgress, "Draw the icons");      // WEB-5
        var before = (await BoardOfAsync("WEB")).Columns.Select(c => (c.Name, string.Join(",", c.Cards.Select(x => x.Key)))).ToList();

        var switched = await SwitchAsync("WEB", ProjectTemplate.Scrum);

        Assert.True(switched.IsSuccess, switched.Error?.Message);
        Assert.Equal(ProjectTemplate.Scrum, switched.Value!.Template);
        Assert.Equal(["WEB-2", "WEB-4", "WEB-1", "WEB-5", "WEB-3"], await CardsByRankAsync("WEB"));
        // No card moves: each column keeps its cards in their order (the Scrum board itself waits for a sprint).
        Assert.Equal(before, await ColumnsByRankAsync("WEB"));
        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.ProjectTemplateChanged);
        Assert.Equal(("WEB", _amina.Id), (audit.Target, audit.ActorUserId));
        Assert.Equal("{\"From\":\"Kanban\",\"To\":\"Scrum\"}", audit.Details);
    }

    [Fact]
    public async Task P3_US1_AS9_No_project_switches_to_or_from_Stage_gate()
    {
        await CreateAsync("Website Revamp", "WEB");
        await CreateAsync("Core Banking Upgrade", "CBU", template: ProjectTemplate.StageGate);

        Assert.Equal(ErrorCodes.TemplateSwitchNotAllowed, (await SwitchAsync("WEB", ProjectTemplate.StageGate)).Error!.Code);
        Assert.Equal(ErrorCodes.TemplateSwitchNotAllowed, (await SwitchAsync("CBU", ProjectTemplate.Kanban)).Error!.Code);
        Assert.Equal(ProjectTemplate.Kanban, (await DetailsAsync("WEB")).Value!.Template);
    }

    [Fact]
    public async Task P3_FR005_A_Scrum_project_switches_back_to_Kanban_only_without_planned_or_active_sprints()
    {
        await CreateAsync("Mobile App", "MOB", template: ProjectTemplate.Scrum);
        var sprint = (await CallAsync<ISprintService, Result<SprintSection>>(s => s.CreateAsync("MOB", Ct))).ValueOrThrow();

        Assert.Equal(ErrorCodes.SprintsOpen, (await SwitchAsync("MOB", ProjectTemplate.Kanban)).Error!.Code);

        var active = (await CallAsync<ISprintService, Result<SprintSection>>(s =>
            s.StartAsync(sprint.Id, new DateOnly(2026, 10, 5), null, null, sprint.Version, Ct))).ValueOrThrow();
        Assert.Equal(ErrorCodes.SprintsOpen, (await SwitchAsync("MOB", ProjectTemplate.Kanban)).Error!.Code);

        (await CallAsync<ISprintService, Result<SprintSection>>(s => s.CompleteAsync(active.Id, null, active.Version, Ct))).ValueOrThrow();
        Assert.True((await SwitchAsync("MOB", ProjectTemplate.Kanban)).IsSuccess);
        Assert.Equal(ProjectTemplate.Kanban, (await DetailsAsync("MOB")).Value!.Template);
    }

    [Fact]
    public async Task P3_FR005_Only_Project_Admins_and_administrators_switch_templates_and_stale_switches_conflict()
    {
        await CreateAsync("Website Revamp", "WEB");
        var version = (await DetailsAsync("WEB")).Value!.DetailsVersion;
        var bilal = await Data.UserAsync("bilal");
        await Data.MembersAsync("WEB", ProjectRole.Member, bilal);

        ActAs(bilal);
        Assert.Equal(ErrorKind.Forbidden, (await SwitchAsync("WEB", ProjectTemplate.Scrum, version)).Error!.Kind);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await SwitchAsync("WEB", ProjectTemplate.Scrum, version)).IsSuccess);

        ActAs(_amina);
        var stale = await SwitchAsync("WEB", ProjectTemplate.Kanban, version);
        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(ProjectTemplate.Scrum, Assert.IsType<ProjectDetails>(stale.Error.Current).Template);
    }

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
        await AddCardAsync("WEB", toDo, "Open one");
        await AddCardAsync("WEB", toDo, "Open two");
        await AddCardAsync("WEB", done, "Finished");

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
