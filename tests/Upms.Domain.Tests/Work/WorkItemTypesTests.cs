using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Work item types, containers, estimates and milestones (Phase 3 FR-007–FR-015; data-model.md
/// "WorkItemType").</summary>
public sealed class WorkItemTypesTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private static ChangeContext At(int minutes = 0) => ChangeContext.New(Amina, T0.AddMinutes(minutes));

    private static WorkItem New(WorkItemType type, ProjectTemplate template = ProjectTemplate.Scrum, int number = 1,
        string title = "Card refunds") =>
        WorkItem.Create(10, "MOB", number, type, title, ToDo, Rank.First(), template, At()).Value!;

    private static ContainerRef RefOf(WorkItem container, long id = 100) =>
        new(id, container.ProjectId, container.Key, container.Title, container.Type);

    // ----- The type table -----

    [Theory]
    [InlineData(WorkItemType.Epic, WorkItemLevel.Container)]
    [InlineData(WorkItemType.Stage, WorkItemLevel.Container)]
    [InlineData(WorkItemType.Story, WorkItemLevel.Standard)]
    [InlineData(WorkItemType.Task, WorkItemLevel.Standard)]
    [InlineData(WorkItemType.Bug, WorkItemLevel.Standard)]
    [InlineData(WorkItemType.Milestone, WorkItemLevel.Standard)]
    [InlineData(WorkItemType.Subtask, WorkItemLevel.Subtask)]
    public void Each_type_has_its_level(WorkItemType type, WorkItemLevel level)
    {
        Assert.Equal(level, WorkItemTypes.LevelOf(type));
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban, new[] { WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask })]
    [InlineData(ProjectTemplate.Scrum, new[] { WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask })]
    [InlineData(ProjectTemplate.StageGate, new[] { WorkItemType.Stage, WorkItemType.Task, WorkItemType.Milestone, WorkItemType.Subtask })]
    public void P3_FR007_Each_template_offers_its_types(ProjectTemplate template, WorkItemType[] offered)
    {
        Assert.Equal(offered.Order(), Enum.GetValues<WorkItemType>().Where(t => WorkItemTypes.IsOffered(t, template, WorkTypeSet.All)).Order());
    }

    [Fact]
    public void P3_FR013_Only_stories_tasks_and_bugs_are_cards()
    {
        Assert.Equal([WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug], WorkItemTypes.CardTypes);
        Assert.All(Enum.GetValues<WorkItemType>(), t => Assert.Equal(WorkItemTypes.CardTypes.Contains(t), WorkItemTypes.IsCard(t)));
    }

    // ----- Creating -----

    [Fact]
    public void P3_US1_AS4_An_epic_is_created_with_its_key_and_a_Created_entry()
    {
        var epic = New(WorkItemType.Epic);

        Assert.Equal(WorkItemType.Epic, epic.Type);
        Assert.Equal("MOB-1", epic.Key);
        Assert.Null(epic.ContainerId);
        Assert.Equal(WorkItemField.Created, Assert.Single(epic.Changes).Field);
    }

    [Theory]
    [InlineData(WorkItemType.Stage, ProjectTemplate.Scrum)]
    [InlineData(WorkItemType.Milestone, ProjectTemplate.Kanban)]
    [InlineData(WorkItemType.Epic, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Story, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Bug, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Subtask, ProjectTemplate.Scrum)]
    public void P3_FR007_A_type_the_template_does_not_offer_is_refused(WorkItemType type, ProjectTemplate template)
    {
        var result = WorkItem.Create(10, "MOB", 1, type, "Something", ToDo, Rank.First(), template, At());

        Assert.Equal(WorkItem.TypeNotAllowedCode, result.Error!.Code);
    }

    // ----- Changing the type -----

    [Theory]
    [InlineData(WorkItemType.Bug, WorkItemType.Story)]
    [InlineData(WorkItemType.Story, WorkItemType.Task)]
    [InlineData(WorkItemType.Task, WorkItemType.Bug)]
    public void P3_US1_AS6_Stories_tasks_and_bugs_change_into_each_other_with_history(WorkItemType from, WorkItemType to)
    {
        var item = New(from);

        Assert.Null(item.ChangeType(to, ProjectTemplate.Scrum, At(5)));

        Assert.Equal(to, item.Type);
        Assert.Equal(T0.AddMinutes(5), item.UpdatedAt);
        var change = item.Changes.Last();
        Assert.Equal(WorkItemField.Type, change.Field);
        Assert.Equal((from.ToString(), to.ToString()), (change.OldValue, change.NewValue));
    }

    [Theory]
    [InlineData(WorkItemType.Epic, WorkItemType.Story, ProjectTemplate.Scrum)]
    [InlineData(WorkItemType.Story, WorkItemType.Epic, ProjectTemplate.Scrum)]
    [InlineData(WorkItemType.Stage, WorkItemType.Task, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Milestone, WorkItemType.Task, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Task, WorkItemType.Milestone, ProjectTemplate.StageGate)]
    public void P3_US1_AS6_Other_type_changes_are_refused(WorkItemType from, WorkItemType to, ProjectTemplate template)
    {
        var item = New(from, template);

        Assert.Equal(WorkItem.TypeChangeNotAllowedCode, item.ChangeType(to, template, At(5))!.Code);
        Assert.Equal(from, item.Type);
    }

    [Fact]
    public void A_task_in_a_Stage_gate_project_cannot_become_a_story()
    {
        var task = New(WorkItemType.Task, ProjectTemplate.StageGate);

        Assert.Equal(WorkItem.TypeChangeNotAllowedCode, task.ChangeType(WorkItemType.Story, ProjectTemplate.StageGate, At())!.Code);
    }

    [Fact]
    public void Type_choices_list_the_types_an_item_can_become()
    {
        Assert.Equal([WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug], WorkItemTypes.ChoicesFor(WorkItemType.Bug, ProjectTemplate.Kanban));
        Assert.Empty(WorkItemTypes.ChoicesFor(WorkItemType.Epic, ProjectTemplate.Scrum));
        Assert.Empty(WorkItemTypes.ChoicesFor(WorkItemType.Task, ProjectTemplate.StageGate));
        Assert.Empty(WorkItemTypes.ChoicesFor(WorkItemType.Subtask, ProjectTemplate.Scrum));
    }

    [Fact]
    public void Changing_to_the_same_type_records_nothing()
    {
        var story = New(WorkItemType.Story);

        Assert.Null(story.ChangeType(WorkItemType.Story, ProjectTemplate.Scrum, At(1)));

        Assert.Single(story.Changes);
    }

    // ----- Containers -----

    [Fact]
    public void P3_US1_AS5_A_story_goes_into_an_epic_and_moves_out_again_with_history()
    {
        var epic = New(WorkItemType.Epic, number: 1, title: "Card refunds");
        var story = New(WorkItemType.Story, number: 2, title: "Refund to card");
        var container = RefOf(epic);

        Assert.Null(story.PlaceIn(null, container, At(1)));
        Assert.Equal(container.Id, story.ContainerId);
        var placed = story.Changes.Last();
        Assert.Equal(WorkItemField.Container, placed.Field);
        Assert.Equal((null, "MOB-1 Card refunds"), (placed.OldValue, placed.NewValue));

        Assert.Null(story.PlaceIn(container, null, At(2)));
        Assert.Null(story.ContainerId);
        Assert.Equal(("MOB-1 Card refunds", null), (story.Changes.Last().OldValue, story.Changes.Last().NewValue));
    }

    [Fact]
    public void A_milestone_and_a_task_go_into_a_stage()
    {
        var stage = New(WorkItemType.Stage, ProjectTemplate.StageGate, 1, "Planning");
        var milestone = New(WorkItemType.Milestone, ProjectTemplate.StageGate, 2, "Planning sign-off");

        Assert.Null(milestone.PlaceIn(null, RefOf(stage), At(1)));
        Assert.Equal(100, milestone.ContainerId);
    }

    [Fact]
    public void P3_FR008_Only_an_epic_or_a_stage_can_hold_items()
    {
        var story = New(WorkItemType.Story, number: 1);
        var other = New(WorkItemType.Task, number: 2);

        Assert.Equal(WorkItem.InvalidContainerCode, story.PlaceIn(null, RefOf(other), At())!.Code);
    }

    [Fact]
    public void P3_FR008_Containers_and_sub_tasks_have_no_container()
    {
        var epic = New(WorkItemType.Epic, number: 1);
        var second = New(WorkItemType.Epic, number: 2);

        Assert.Equal(WorkItem.InvalidContainerCode, second.PlaceIn(null, RefOf(epic), At())!.Code);
    }

    [Fact]
    public void P3_FR008_A_container_must_be_in_the_same_project()
    {
        var story = New(WorkItemType.Story, number: 1);
        var elsewhere = new ContainerRef(100, 99, "PAY-1", "Payments", WorkItemType.Epic);

        Assert.Equal(WorkItem.InvalidContainerCode, story.PlaceIn(null, elsewhere, At())!.Code);
    }

    [Fact]
    public void Placing_an_item_where_it_already_is_records_nothing()
    {
        var epic = New(WorkItemType.Epic, number: 1);
        var story = New(WorkItemType.Story, number: 2);
        story.PlaceIn(null, RefOf(epic), At(1));
        var count = story.Changes.Count;

        Assert.Null(story.PlaceIn(RefOf(epic), RefOf(epic), At(2)));

        Assert.Equal(count, story.Changes.Count);
    }

    // ----- Estimates -----

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(999.9)]
    public void P3_FR014_Stories_take_an_estimate_from_0_to_999_9(double value)
    {
        var story = New(WorkItemType.Story);

        Assert.Null(story.SetEstimate((decimal)value, ProjectTemplate.Scrum, At(1)));

        Assert.Equal((decimal)value, story.Estimate);
        var change = story.Changes.Last();
        Assert.Equal(WorkItemField.Estimate, change.Field);
        Assert.Equal(((decimal)value).ToString(System.Globalization.CultureInfo.InvariantCulture), change.NewValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(2.55)]
    public void P3_FR014_An_estimate_out_of_range_or_with_two_decimals_is_refused(double value)
    {
        var story = New(WorkItemType.Story);

        var error = story.SetEstimate((decimal)value, ProjectTemplate.Scrum, At(1));

        Assert.Equal("Estimate", error!.Field);
        Assert.Null(story.Estimate);
    }

    [Fact]
    public void An_estimate_can_be_cleared()
    {
        var bug = New(WorkItemType.Bug);
        bug.SetEstimate(3, ProjectTemplate.Kanban, At(1));

        Assert.Null(bug.SetEstimate(null, ProjectTemplate.Kanban, At(2)));

        Assert.Null(bug.Estimate);
        Assert.Equal(("3", null), (bug.Changes.Last().OldValue, bug.Changes.Last().NewValue));
    }

    [Theory]
    [InlineData(WorkItemType.Epic, ProjectTemplate.Scrum)]
    [InlineData(WorkItemType.Stage, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Milestone, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Task, ProjectTemplate.StageGate)]
    public void P3_FR014_Only_cards_of_agile_projects_have_an_estimate(WorkItemType type, ProjectTemplate template)
    {
        var item = New(type, template);

        Assert.Equal("Estimate", item.SetEstimate(3, template, At(1))!.Field);
    }

    // ----- Milestones and sub-tasks -----

    [Fact]
    public void P3_US1_AS7_A_milestone_has_a_single_date()
    {
        var milestone = New(WorkItemType.Milestone, ProjectTemplate.StageGate, title: "Planning sign-off");

        Assert.Null(milestone.Schedule(null, new DateOnly(2026, 10, 30), At(1)));
        Assert.Equal(new DateOnly(2026, 10, 30), milestone.DueDate);

        var error = milestone.Schedule(new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30), At(2));
        Assert.Equal((WorkItem.InvalidDatesCode, "StartDate"), (error!.Code, error.Field));
        Assert.Null(milestone.StartDate);
    }

    [Theory]
    [InlineData(WorkItemType.Milestone, ProjectTemplate.StageGate)]
    [InlineData(WorkItemType.Epic, ProjectTemplate.Scrum)]
    [InlineData(WorkItemType.Stage, ProjectTemplate.StageGate)]
    public void P3_US1_AS7_Milestones_epics_and_stages_have_no_sub_tasks(WorkItemType type, ProjectTemplate template)
    {
        var item = New(type, template);

        Assert.Equal(WorkItem.NoSubtasksCode, item.AddSubtask(2, "Something", ToDo, Rank.First(), At(1)).Error!.Code);
    }

    [Theory]
    [InlineData(WorkItemType.Story)]
    [InlineData(WorkItemType.Bug)]
    public void Stories_and_bugs_have_sub_tasks(WorkItemType type)
    {
        var item = New(type);

        var subtask = item.AddSubtask(2, "Write the test", ToDo, Rank.First(), At(1)).Value!;

        Assert.Equal(WorkItemType.Subtask, subtask.Type);
    }

    [Fact]
    public void Sub_tasks_still_have_no_sub_tasks_of_their_own()
    {
        var story = New(WorkItemType.Story);
        var subtask = story.AddSubtask(2, "Write the test", ToDo, Rank.First(), At(1)).Value!;

        Assert.Equal(WorkItem.SubtaskDepthCode, subtask.AddSubtask(3, "Deeper", ToDo, Rank.First(), At(2)).Error!.Code);
    }
}
