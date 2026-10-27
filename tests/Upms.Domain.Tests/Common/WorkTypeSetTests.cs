using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Common;

/// <summary>The work types a space has turned on (Jira parity pass FR-017; data-model.md "WorkTypeSet"; research R14).</summary>
public sealed class WorkTypeSetTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void All_is_the_four_types()
    {
        Assert.Equal(WorkTypeSet.Epic | WorkTypeSet.Story | WorkTypeSet.Task | WorkTypeSet.Bug, WorkTypeSet.All);
        Assert.Equal(15, (int)WorkTypeSet.All);
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban)]
    [InlineData(ProjectTemplate.Scrum)]
    public void JP_FR017_Agile_spaces_offer_the_types_that_are_on_and_always_subtasks(ProjectTemplate template)
    {
        var set = WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug;

        Assert.Equal(
            new[] { WorkItemType.Epic, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask }.Order(),
            Enum.GetValues<WorkItemType>().Where(t => WorkItemTypes.IsOffered(t, template, set)).Order());
    }

    [Fact]
    public void JP_FR017_Stage_gate_spaces_keep_their_types_whatever_the_set()
    {
        Assert.Equal(
            new[] { WorkItemType.Task, WorkItemType.Stage, WorkItemType.Milestone, WorkItemType.Subtask }.Order(),
            Enum.GetValues<WorkItemType>().Where(t => WorkItemTypes.IsOffered(t, ProjectTemplate.StageGate, WorkTypeSet.Story)).Order());
    }

    [Fact]
    public void JP_EdgeCase_A_type_turned_off_cannot_be_created()
    {
        var created = WorkItem.Create(10, "DATA", 1, WorkItemType.Bug, "Broken export", ToDo, Rank.First(), ProjectTemplate.Kanban,
            ChangeContext.New(Guid.NewGuid(), T0), WorkTypeSet.Epic | WorkTypeSet.Task);

        Assert.False(created.IsSuccess);
        Assert.Equal(WorkItem.TypeNotAllowedCode, created.Error!.Code);
        Assert.Equal("This space no longer uses Bugs.", created.Error.Message);
    }

    [Fact]
    public void A_type_the_template_does_not_have_is_refused_in_space_words()
    {
        var created = WorkItem.Create(10, "WEB", 1, WorkItemType.Stage, "Initiation", ToDo, Rank.First(), ProjectTemplate.Kanban,
            ChangeContext.New(Guid.NewGuid(), T0));

        Assert.Equal("This space's template does not use Stages.", created.Error!.Message);
    }

    [Theory]
    [InlineData(WorkItemType.Epic, "Epics")]
    [InlineData(WorkItemType.Story, "Stories")]
    [InlineData(WorkItemType.Task, "Tasks")]
    [InlineData(WorkItemType.Bug, "Bugs")]
    [InlineData(WorkItemType.Stage, "Stages")]
    [InlineData(WorkItemType.Milestone, "Milestones")]
    [InlineData(WorkItemType.Subtask, "Sub-tasks")]
    public void Each_type_has_a_plural_name(WorkItemType type, string plural)
    {
        Assert.Equal(plural, WorkItemTypes.PluralName(type));
    }

    [Theory]
    [InlineData(WorkItemType.Epic, WorkTypeSet.Epic)]
    [InlineData(WorkItemType.Story, WorkTypeSet.Story)]
    [InlineData(WorkItemType.Task, WorkTypeSet.Task)]
    [InlineData(WorkItemType.Bug, WorkTypeSet.Bug)]
    [InlineData(WorkItemType.Subtask, WorkTypeSet.None)]
    [InlineData(WorkItemType.Stage, WorkTypeSet.None)]
    public void Each_switchable_type_has_its_flag(WorkItemType type, WorkTypeSet flag)
    {
        Assert.Equal(flag, WorkTypeSets.FlagOf(type));
    }
}
