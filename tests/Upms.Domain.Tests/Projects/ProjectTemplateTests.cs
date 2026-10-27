using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>A project's template decides its starting columns and can switch only between Kanban and Scrum (Phase 3
/// FR-001, FR-002, FR-005; data-model.md "Template rules").</summary>
public sealed class ProjectTemplateTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private static Project Create(ProjectTemplate template) =>
        Project.Create("Mobile App", "MOB", null, Owner, Now, template).Value!;

    private static (string, StatusCategory, int) Column(ProjectStatus s) => (s.Name, s.Category, s.Position);

    [Fact]
    public void P3_US1_AS1_Kanban_is_the_template_when_none_is_chosen()
    {
        var project = Project.Create("Website Revamp", "WEB", null, Owner, Now).Value!;

        Assert.Equal(ProjectTemplate.Kanban, project.Template);
        Assert.Equal(["To Do", "In Progress", "Done"], project.Statuses.Select(s => s.Name));
    }

    [Fact]
    public void P3_US1_AS2_A_Scrum_project_adds_In_Review_before_Done()
    {
        var project = Create(ProjectTemplate.Scrum);

        Assert.Equal(ProjectTemplate.Scrum, project.Template);
        Assert.Collection(project.Statuses,
            s => Assert.Equal(("To Do", StatusCategory.ToDo, 0), Column(s)),
            s => Assert.Equal(("In Progress", StatusCategory.InProgress, 1), Column(s)),
            s => Assert.Equal(("In Review", StatusCategory.InProgress, 2), Column(s)),
            s => Assert.Equal(("Done", StatusCategory.Done, 3), Column(s)));
    }

    [Fact]
    public void P3_US1_AS2_A_Stage_gate_project_starts_with_the_structured_columns()
    {
        var project = Create(ProjectTemplate.StageGate);

        Assert.Equal(ProjectTemplate.StageGate, project.Template);
        Assert.Collection(project.Statuses,
            s => Assert.Equal(("Not Started", StatusCategory.ToDo, 0), Column(s)),
            s => Assert.Equal(("In Progress", StatusCategory.InProgress, 1), Column(s)),
            s => Assert.Equal(("On Hold", StatusCategory.InProgress, 2), Column(s)),
            s => Assert.Equal(("Completed", StatusCategory.Done, 3), Column(s)));
    }

    [Fact]
    public void P3_US1_AS9_Kanban_switches_to_Scrum_keeping_its_columns()
    {
        var project = Create(ProjectTemplate.Kanban);
        var columns = project.Statuses.Select(Column).ToList();
        var version = project.DetailsVersion;

        Assert.Null(project.SwitchTemplate(ProjectTemplate.Scrum, hasOpenSprints: false, Now.AddHours(1)));

        Assert.Equal(ProjectTemplate.Scrum, project.Template);
        Assert.Equal(columns, project.Statuses.Select(Column));
        Assert.Equal(version + 1, project.DetailsVersion);
        Assert.Equal(Now.AddHours(1), project.UpdatedAt);
    }

    [Fact]
    public void Scrum_switches_back_to_Kanban_when_no_sprint_is_active_or_planned()
    {
        var project = Create(ProjectTemplate.Scrum);

        Assert.Null(project.SwitchTemplate(ProjectTemplate.Kanban, hasOpenSprints: false, Now));

        Assert.Equal(ProjectTemplate.Kanban, project.Template);
    }

    [Fact]
    public void Scrum_cannot_switch_to_Kanban_while_a_sprint_is_active_or_planned()
    {
        var project = Create(ProjectTemplate.Scrum);
        var version = project.DetailsVersion;

        var error = project.SwitchTemplate(ProjectTemplate.Kanban, hasOpenSprints: true, Now);

        Assert.Equal(Project.SprintsOpenCode, error!.Code);
        Assert.Equal(ProjectTemplate.Scrum, project.Template);
        Assert.Equal(version, project.DetailsVersion);
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban, ProjectTemplate.StageGate)]
    [InlineData(ProjectTemplate.Scrum, ProjectTemplate.StageGate)]
    [InlineData(ProjectTemplate.StageGate, ProjectTemplate.Kanban)]
    [InlineData(ProjectTemplate.StageGate, ProjectTemplate.Scrum)]
    public void P3_US1_AS9_No_project_switches_to_or_from_Stage_gate(ProjectTemplate from, ProjectTemplate to)
    {
        var project = Create(from);

        var error = project.SwitchTemplate(to, hasOpenSprints: false, Now);

        Assert.Equal(Project.TemplateSwitchNotAllowedCode, error!.Code);
        Assert.Equal(from, project.Template);
    }

    [Fact]
    public void Switching_to_the_same_template_changes_nothing()
    {
        var project = Create(ProjectTemplate.Scrum);
        var version = project.DetailsVersion;

        Assert.Null(project.SwitchTemplate(ProjectTemplate.Scrum, hasOpenSprints: true, Now));

        Assert.Equal(version, project.DetailsVersion);
    }
}
