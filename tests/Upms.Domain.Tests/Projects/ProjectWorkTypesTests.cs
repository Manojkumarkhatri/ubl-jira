using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>A space's work types, turned on and off by Space admins (Jira parity pass FR-017; data-model.md "Project").</summary>
public sealed class ProjectWorkTypesTests
{
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<WorkTypeSet, int> NoneUsed = new Dictionary<WorkTypeSet, int>();

    private static Project Space(ProjectTemplate template = ProjectTemplate.Kanban) =>
        Project.Create("Website Revamp", "WEB", null, Amina, T0, template).Value!;

    [Fact]
    public void JP_US3_AS6_Types_are_turned_on_and_off_and_the_details_version_moves()
    {
        var space = Space();
        var version = space.DetailsVersion;

        Assert.Null(space.SetWorkTypes(WorkTypeSet.Story | WorkTypeSet.Bug, NoneUsed, T0.AddMinutes(1)));

        Assert.Equal(WorkTypeSet.Story | WorkTypeSet.Bug, space.WorkTypes);
        Assert.Equal(version + 1, space.DetailsVersion);
        Assert.Equal(T0.AddMinutes(1), space.UpdatedAt);
    }

    [Fact]
    public void JP_US3_AS6_A_type_in_use_cannot_be_turned_off()
    {
        var space = Space();

        var refused = space.SetWorkTypes(WorkTypeSet.Story | WorkTypeSet.Task, new Dictionary<WorkTypeSet, int> { [WorkTypeSet.Bug] = 4 }, T0);

        Assert.Equal(Project.WorkTypeInUseCode, refused!.Code);
        Assert.Equal("Bugs are still used by 4 items in this space.", refused.Message);
        Assert.Equal(WorkTypeSet.All, space.WorkTypes);
    }

    [Fact]
    public void One_item_reads_in_the_singular_and_a_type_that_stays_on_may_be_in_use()
    {
        var space = Space();

        Assert.Equal("Stories are still used by 1 item in this space.",
            space.SetWorkTypes(WorkTypeSet.Task, new Dictionary<WorkTypeSet, int> { [WorkTypeSet.Story] = 1 }, T0)!.Message);
        Assert.Null(space.SetWorkTypes(WorkTypeSet.Task | WorkTypeSet.Story, new Dictionary<WorkTypeSet, int> { [WorkTypeSet.Story] = 1 }, T0));
    }

    [Fact]
    public void A_space_keeps_a_story_task_or_bug_type()
    {
        Assert.Equal(Project.NoStandardTypeCode, Space().SetWorkTypes(WorkTypeSet.Epic, NoneUsed, T0)!.Code);
    }

    [Fact]
    public void Stage_gate_spaces_have_fixed_types()
    {
        Assert.Equal(Project.TemplateSwitchNotAllowedCode, Space(ProjectTemplate.StageGate).SetWorkTypes(WorkTypeSet.Task, NoneUsed, T0)!.Code);
    }

    [Fact]
    public void Switching_between_Kanban_and_Scrum_keeps_the_types()
    {
        var space = Space();
        space.SetWorkTypes(WorkTypeSet.Story | WorkTypeSet.Task, NoneUsed, T0);

        Assert.Null(space.SwitchTemplate(ProjectTemplate.Scrum, hasOpenSprints: false, T0));

        Assert.Equal(WorkTypeSet.Story | WorkTypeSet.Task, space.WorkTypes);
    }
}
