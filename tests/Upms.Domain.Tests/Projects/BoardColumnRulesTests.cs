using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>Board column rules enforced by the project (FR-034 to FR-039, FR-041, data-model.md "Board rules").</summary>
public sealed class BoardColumnRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    private static Project NewProject() => Project.Create("Website Revamp", "WEB", null, Guid.NewGuid(), Now).Value!;

    private static ProjectStatus Column(Project project, string name) => project.Statuses.Single(s => s.Name == name);

    private static string[] Names(Project project) => project.Statuses.Select(s => s.Name).ToArray();

    private static void AssertConsecutive(Project project) =>
        Assert.Equal(Enumerable.Range(0, project.Statuses.Count), project.Statuses.Select(s => s.Position));

    [Fact]
    public void US3_AS1_A_column_is_added_at_the_chosen_position()
    {
        var project = NewProject();

        var result = project.AddColumn("In Review", StatusCategory.InProgress, 2, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], Names(project));
        Assert.Equal(StatusCategory.InProgress, result.Value!.Category);
        Assert.Null(result.Value.WipLimit);
        AssertConsecutive(project);
        Assert.Equal(2, project.BoardVersion);
    }

    [Theory]
    [InlineData(-5, new[] { "Blocked", "To Do", "In Progress", "Done" })]
    [InlineData(0, new[] { "Blocked", "To Do", "In Progress", "Done" })]
    [InlineData(3, new[] { "To Do", "In Progress", "Done", "Blocked" })]
    [InlineData(99, new[] { "To Do", "In Progress", "Done", "Blocked" })]
    public void Positions_outside_the_board_mean_first_or_last(int position, string[] expected)
    {
        var project = NewProject();

        project.AddColumn("Blocked", StatusCategory.InProgress, position, Now);

        Assert.Equal(expected, Names(project));
        AssertConsecutive(project);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that is thirty-one chars")]
    public void Names_have_1_to_30_characters(string name)
    {
        var project = NewProject();

        var error = project.AddColumn(name, StatusCategory.InProgress, 1, Now).Error;

        Assert.NotNull(error);
        Assert.Equal("Name", error.Field);
        Assert.Equal(3, project.Statuses.Count);
        Assert.Equal(1, project.BoardVersion);
    }

    [Fact]
    public void Names_are_trimmed_and_thirty_characters_are_allowed()
    {
        var project = NewProject();

        var added = project.AddColumn("  Waiting for the customer ok  ", StatusCategory.InProgress, 2, Now).Value!;

        Assert.Equal("Waiting for the customer ok", added.Name);
        Assert.True(project.AddColumn(new string('x', 30), StatusCategory.InProgress, 2, Now).IsSuccess);
    }

    [Fact]
    public void US3_AS7_A_name_already_on_the_board_is_refused_ignoring_case()
    {
        var project = NewProject();

        var added = project.AddColumn("done", StatusCategory.Done, 3, Now).Error;
        var renamed = project.RenameColumn(Column(project, "In Progress"), " TO DO ", Now);

        Assert.Equal(Project.DuplicateColumnNameCode, added?.Code);
        Assert.Equal(Project.DuplicateColumnNameCode, renamed?.Code);
        Assert.Equal(["To Do", "In Progress", "Done"], Names(project));
        Assert.Equal(1, project.BoardVersion);
    }

    [Fact]
    public void US3_AS2_Renaming_keeps_the_columns_position_type_and_identity()
    {
        var project = NewProject();
        var toDo = Column(project, "To Do");

        Assert.Null(project.RenameColumn(toDo, "Backlog", Now));

        Assert.Same(toDo, project.Statuses[0]);
        Assert.Equal(("Backlog", "BACKLOG", StatusCategory.ToDo, 0), (toDo.Name, toDo.NormalizedName, toDo.Category, toDo.Position));
        Assert.Equal(2, project.BoardVersion);
    }

    [Fact]
    public void A_column_can_change_the_letter_case_of_its_own_name()
    {
        var project = NewProject();

        Assert.Null(project.RenameColumn(Column(project, "Done"), "DONE", Now));

        Assert.Equal("DONE", project.Statuses[2].Name);
    }

    [Fact]
    public void A_board_holds_at_most_ten_columns()
    {
        var project = NewProject();
        for (var i = 1; i <= 7; i++)
        {
            Assert.True(project.AddColumn($"Step {i}", StatusCategory.InProgress, 2, Now).IsSuccess);
        }

        var error = project.AddColumn("One too many", StatusCategory.InProgress, 2, Now).Error;

        Assert.Equal(Project.TooManyColumnsCode, error?.Code);
        Assert.Equal(10, project.Statuses.Count);
    }

    [Fact]
    public void US3_AS3_Moving_a_column_keeps_positions_consecutive()
    {
        var project = NewProject();
        project.AddColumn("In Review", StatusCategory.InProgress, 2, Now);

        Assert.Null(project.MoveColumn(Column(project, "Done"), 0, Now));
        Assert.Equal(["Done", "To Do", "In Progress", "In Review"], Names(project));
        Assert.Null(project.MoveColumn(Column(project, "Done"), 99, Now));
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], Names(project));
        Assert.Null(project.MoveColumn(Column(project, "To Do"), 2, Now));

        Assert.Equal(["In Progress", "In Review", "To Do", "Done"], Names(project));
        AssertConsecutive(project);
        Assert.Equal(5, project.BoardVersion);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(99)]
    public void Limits_are_1_to_99(int limit)
    {
        var project = NewProject();
        var inProgress = Column(project, "In Progress");

        Assert.Null(project.SetWipLimit(inProgress, limit, Now));

        Assert.Equal(limit, inProgress.WipLimit);
        Assert.Equal(2, project.BoardVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Limits_outside_1_to_99_are_refused(int limit)
    {
        var project = NewProject();

        var error = project.SetWipLimit(Column(project, "In Progress"), limit, Now);

        Assert.Equal("WipLimit", error?.Field);
        Assert.Null(Column(project, "In Progress").WipLimit);
    }

    [Fact]
    public void A_limit_can_be_removed()
    {
        var project = NewProject();
        var inProgress = Column(project, "In Progress");
        project.SetWipLimit(inProgress, 3, Now);

        Assert.Null(project.SetWipLimit(inProgress, null, Now));

        Assert.Null(inProgress.WipLimit);
    }

    [Fact]
    public void A_columns_type_changes_only_while_it_is_empty()
    {
        var project = NewProject();
        var inProgress = Column(project, "In Progress");

        var refused = project.ChangeColumnCategory(inProgress, StatusCategory.ToDo, itemsInColumn: 1, Now);
        var allowed = project.ChangeColumnCategory(inProgress, StatusCategory.ToDo, itemsInColumn: 0, Now);

        Assert.Equal(Project.ColumnNotEmptyCode, refused?.Code);
        Assert.Null(allowed);
        Assert.Equal(StatusCategory.ToDo, inProgress.Category);
        Assert.Equal(2, project.BoardVersion);
    }

    [Fact]
    public void US3_AS6_The_last_to_do_and_the_last_done_column_cannot_be_retyped_or_deleted()
    {
        var project = NewProject();
        var toDo = Column(project, "To Do");
        var done = Column(project, "Done");
        var inProgress = Column(project, "In Progress");

        Assert.Equal(Project.LastToDoColumnCode, project.ChangeColumnCategory(toDo, StatusCategory.InProgress, 0, Now)?.Code);
        Assert.Equal(Project.LastDoneColumnCode, project.ChangeColumnCategory(done, StatusCategory.InProgress, 0, Now)?.Code);
        Assert.Equal(Project.LastToDoColumnCode, project.RemoveColumn(toDo, inProgress, 0, Now)?.Code);
        Assert.Equal(Project.LastDoneColumnCode, project.RemoveColumn(done, inProgress, 0, Now)?.Code);
        Assert.Equal(3, project.Statuses.Count);
        Assert.Equal(1, project.BoardVersion);
    }

    [Fact]
    public void A_to_do_or_done_column_can_go_when_another_of_its_type_remains()
    {
        var project = NewProject();
        project.AddColumn("Ideas", StatusCategory.ToDo, 0, Now);
        project.AddColumn("Archived", StatusCategory.Done, 4, Now);

        Assert.Null(project.RemoveColumn(Column(project, "To Do"), null, 0, Now));
        Assert.Null(project.ChangeColumnCategory(Column(project, "Done"), StatusCategory.InProgress, 0, Now));

        Assert.Equal(["Ideas", "In Progress", "Done", "Archived"], Names(project));
        AssertConsecutive(project);
    }

    [Fact]
    public void Deleting_a_column_that_holds_work_items_requires_a_destination()
    {
        var project = NewProject();
        project.AddColumn("In Review", StatusCategory.InProgress, 2, Now);
        var review = Column(project, "In Review");

        var refused = project.RemoveColumn(review, null, itemsInColumn: 2, Now);
        var toItself = project.RemoveColumn(review, review, itemsInColumn: 2, Now);
        var removed = project.RemoveColumn(review, Column(project, "Done"), itemsInColumn: 2, Now);

        Assert.Equal(Project.DestinationRequiredCode, refused?.Code);
        Assert.Equal("DestinationColumnId", toItself?.Field);
        Assert.Null(removed);
        Assert.Equal(["To Do", "In Progress", "Done"], Names(project));
        AssertConsecutive(project);
        Assert.Equal(3, project.BoardVersion);
    }

    [Fact]
    public void An_empty_column_is_deleted_without_a_destination()
    {
        var project = NewProject();
        project.AddColumn("Blocked", StatusCategory.InProgress, 1, Now);

        Assert.Null(project.RemoveColumn(Column(project, "Blocked"), null, itemsInColumn: 0, Now));

        Assert.Equal(["To Do", "In Progress", "Done"], Names(project));
    }

    [Fact]
    public void Changes_that_change_nothing_leave_the_board_version_alone()
    {
        var project = NewProject();
        var inProgress = Column(project, "In Progress");

        Assert.Null(project.RenameColumn(inProgress, "In Progress", Now));
        Assert.Null(project.MoveColumn(inProgress, 1, Now));
        Assert.Null(project.SetWipLimit(inProgress, null, Now));
        Assert.Null(project.ChangeColumnCategory(inProgress, StatusCategory.InProgress, 5, Now));

        Assert.Equal(1, project.BoardVersion);
    }
}
