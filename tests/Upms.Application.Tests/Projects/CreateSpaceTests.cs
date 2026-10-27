using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Projects;

/// <summary>Creating spaces from the gallery, the Blank template included (Jira parity pass US3, FR-014–FR-018).</summary>
public sealed class CreateSpaceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly SpaceSetup DataSetup = new(
        [new NewColumn("To do", StatusCategory.ToDo), new NewColumn("Building", StatusCategory.InProgress),
         new NewColumn("Validating", StatusCategory.InProgress), new NewColumn("Done", StatusCategory.Done)],
        WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug);

    private User _amina = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _amina = await Data.UserAsync("amina");
        ActAs(_amina);
    }

    private Task<Result<string>> CreateBlankAsync(string name = "Data Platform", string key = "DATA", SpaceSetup? setup = null) =>
        CallAsync<IProjectService, Result<string>>(s => s.CreateBlankAsync(name, key, "Pipelines and models", setup ?? DataSetup, Ct));

    [Theory]
    [InlineData(ProjectTemplate.Kanban)]
    [InlineData(ProjectTemplate.Scrum)]
    [InlineData(ProjectTemplate.StageGate)]
    public async Task JP_US3_AS3_Every_template_makes_the_creator_its_Space_admin(ProjectTemplate template)
    {
        var key = (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Website Revamp", "WEB", null, template, Ct))).ValueOrThrow();

        var team = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync(key, Ct))).ValueOrThrow();
        Assert.Equal((_amina.Id, ProjectRole.ProjectAdmin), (Assert.Single(team.Members).UserId, team.Members[0].Role));
    }

    [Fact]
    public async Task JP_US3_AS4_A_blank_space_is_Kanban_with_exactly_the_chosen_columns_and_types()
    {
        Assert.Equal("DATA", (await CreateBlankAsync()).ValueOrThrow());

        var details = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("DATA", Ct))).ValueOrThrow();
        Assert.Equal((ProjectTemplate.Kanban, WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug), (details.Template, details.WorkTypes));
        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("DATA", false, Ct))).ValueOrThrow();
        Assert.Equal(["To do", "Building", "Validating", "Done"], board.Columns.Select(c => c.Name));
        var spaces = (await CallAsync<ICreateWorkService, Result<CreateSpaces>>(s => s.SpacesAsync("DATA", Ct))).ValueOrThrow();
        Assert.Equal([Domain.Work.WorkItemType.Epic, Domain.Work.WorkItemType.Task, Domain.Work.WorkItemType.Bug, Domain.Work.WorkItemType.Subtask],
            spaces.Spaces.Single().Types);
    }

    [Fact]
    public async Task Refused_setups_create_nothing()
    {
        var noDone = await CreateBlankAsync(setup: new([new NewColumn("To do", StatusCategory.ToDo)], WorkTypeSet.Task));
        var noStandardType = await CreateBlankAsync(setup: DataSetup with { WorkTypes = WorkTypeSet.Epic });

        Assert.Equal(ErrorCodes.LastDoneColumn, noDone.Error!.Code);
        Assert.Equal(ErrorCodes.NoStandardType, noStandardType.Error!.Code);
        Assert.Equal(0, await QueryAsync(db => db.Projects.CountAsync(Ct)));
    }

    [Fact]
    public async Task JP_US3_AS7_A_new_space_is_hidden_from_non_members_but_not_from_administrators()
    {
        (await CreateBlankAsync()).ValueOrThrow();
        var bilal = await Data.UserAsync("bilal");
        var admin = await Data.AdministratorAsync();

        ActAs(bilal);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("DATA", Ct))).Error!.Kind);
        ActAs(admin);
        Assert.True((await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("DATA", Ct))).IsSuccess);
    }

    [Fact]
    public async Task JP_EdgeCase_A_key_already_taken_is_refused()
    {
        (await CreateBlankAsync()).ValueOrThrow();

        var again = await CreateBlankAsync(name: "Data Science");

        Assert.Equal(ErrorCodes.DuplicateProjectKey, again.Error!.Code);
        Assert.Equal("The key DATA is already used by another space.", again.Error.Message);
    }
}
