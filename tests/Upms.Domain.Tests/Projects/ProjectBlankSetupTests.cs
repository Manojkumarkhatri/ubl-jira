using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>A space made from the Blank template: a Kanban space with the columns and work types its creator chose (Jira
/// parity pass FR-016, FR-017; data-model.md "Project"; research R13).</summary>
public sealed class ProjectBlankSetupTests
{
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static DomainResult<Project> Blank(WorkTypeSet types, params (string Name, StatusCategory Category)[] columns) =>
        Project.Create("Data Platform", "DATA", null, Amina, T0, columns, types);

    [Fact]
    public void JP_US3_AS4_The_columns_keep_their_order_and_status_types_and_the_space_is_Kanban()
    {
        var space = Blank(WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug,
            ("To do", StatusCategory.ToDo), ("Building", StatusCategory.InProgress), ("Validating", StatusCategory.InProgress),
            ("Done", StatusCategory.Done)).Value!;

        Assert.Equal(ProjectTemplate.Kanban, space.Template);
        Assert.Equal(WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug, space.WorkTypes);
        Assert.Equal(
            [("To do", StatusCategory.ToDo, 0), ("Building", StatusCategory.InProgress, 1), ("Validating", StatusCategory.InProgress, 2),
             ("Done", StatusCategory.Done, 3)],
            space.Statuses.Select(s => (s.Name, s.Category, s.Position)));
        Assert.Equal(ProjectRole.ProjectAdmin, Assert.Single(space.Members).Role);
    }

    [Fact]
    public void Names_are_trimmed_and_a_two_column_board_is_enough()
    {
        var space = Blank(WorkTypeSet.Task, ("  Open ", StatusCategory.ToDo), ("Closed", StatusCategory.Done)).Value!;

        Assert.Equal(["Open", "Closed"], space.Statuses.Select(s => s.Name));
    }

    [Fact]
    public void JP_FR016_No_more_than_10_columns_and_at_least_one()
    {
        var eleven = Enumerable.Range(1, 9).Select(i => ($"Step {i}", StatusCategory.InProgress))
            .Prepend(("To do", StatusCategory.ToDo)).Append(("Done", StatusCategory.Done)).ToArray();

        Assert.Equal(Project.TooManyColumnsCode, Blank(WorkTypeSet.Task, eleven).Error!.Code);
        Assert.Equal(Project.LastToDoColumnCode, Blank(WorkTypeSet.Task).Error!.Code);
    }

    [Fact]
    public void JP_FR016_A_board_needs_a_to_do_and_a_done_column()
    {
        Assert.Equal(Project.LastToDoColumnCode, Blank(WorkTypeSet.Task, ("Doing", StatusCategory.InProgress), ("Done", StatusCategory.Done)).Error!.Code);
        Assert.Equal(Project.LastDoneColumnCode, Blank(WorkTypeSet.Task, ("To do", StatusCategory.ToDo), ("Doing", StatusCategory.InProgress)).Error!.Code);
    }

    [Fact]
    public void JP_FR016_Column_names_are_unique_ignoring_case_and_short()
    {
        var duplicate = Blank(WorkTypeSet.Task, ("To do", StatusCategory.ToDo), ("TO DO", StatusCategory.InProgress), ("Done", StatusCategory.Done));
        var empty = Blank(WorkTypeSet.Task, ("To do", StatusCategory.ToDo), (" ", StatusCategory.InProgress), ("Done", StatusCategory.Done));
        var tooLong = Blank(WorkTypeSet.Task, ("To do", StatusCategory.ToDo), (new string('x', 31), StatusCategory.InProgress), ("Done", StatusCategory.Done));

        Assert.Equal(Project.DuplicateColumnNameCode, duplicate.Error!.Code);
        Assert.Equal(("Columns[1].Name", DomainError.ValidationCode), (empty.Error!.Field, empty.Error.Code));
        Assert.Equal("Columns[1].Name", tooLong.Error!.Field);
    }

    [Theory]
    [InlineData(WorkTypeSet.None)]
    [InlineData(WorkTypeSet.Epic)]
    public void JP_FR016_A_space_needs_a_story_task_or_bug_type(WorkTypeSet types)
    {
        var refused = Blank(types, ("To do", StatusCategory.ToDo), ("Done", StatusCategory.Done));

        Assert.Equal(Project.NoStandardTypeCode, refused.Error!.Code);
        Assert.Equal("Choose at least one of Story, Task or Bug.", refused.Error.Message);
    }
}
