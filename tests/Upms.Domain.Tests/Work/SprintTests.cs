using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Sprints and cards joining them (Phase 3 FR-023–FR-025, FR-028, FR-030; data-model.md "Sprint").</summary>
public sealed class SprintTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly StatusRef Done = new(3, "Done", StatusCategory.Done);
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October5 = new(2026, 10, 5);
    private static readonly SprintRef Sprint1 = new(1, "MOB Sprint 1", IsCompleted: false);
    private static readonly SprintRef Sprint2 = new(2, "MOB Sprint 2", IsCompleted: false);
    private static readonly SprintRef Sprint1Completed = Sprint1 with { IsCompleted = true };

    private static ChangeContext At(int minutes = 0) => ChangeContext.New(Amina, T0.AddMinutes(minutes));

    private static Sprint NewSprint(int number = 1) => Sprint.Create(10, "MOB", number, At());

    private static Sprint Active()
    {
        var sprint = NewSprint();
        Assert.Null(sprint.Start(October5, null, "Refunds", T0));
        return sprint;
    }

    private static WorkItem New(WorkItemType type, StatusRef? status = null) =>
        WorkItem.Create(10, "MOB", 2, type, "Refund to card", status ?? ToDo, Rank.First(), ProjectTemplate.Scrum, At()).Value!;

    // ----- Creating and editing -----

    [Fact]
    public void P3_US2_AS3_A_new_sprint_is_planned_and_named_after_the_project()
    {
        var sprint = NewSprint(3);

        Assert.Equal((10L, 3, "MOB Sprint 3", SprintState.Planned), (sprint.ProjectId, sprint.Number, sprint.Name, sprint.State));
        Assert.Null(sprint.Goal);
        Assert.Null(sprint.StartDate);
        Assert.Null(sprint.EndDate);
        Assert.Equal((Amina, T0), (sprint.CreatedById, sprint.CreatedAt));
    }

    [Fact]
    public void A_sprint_is_renamed_and_its_goal_changed()
    {
        var sprint = NewSprint();

        Assert.Null(sprint.Update("  Refunds sprint  ", "  Ship card refunds  "));
        Assert.Equal(("Refunds sprint", "Ship card refunds"), (sprint.Name, sprint.Goal));

        Assert.Null(sprint.Update("Refunds sprint", "   "));
        Assert.Null(sprint.Goal);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    [InlineData("A name that is far too long for a sprint, since sprints have short names", "Name")]
    public void P3_FR023_Sprint_names_are_required_and_at_most_60_characters(string name, string field)
    {
        var error = NewSprint().Update(name, null);

        Assert.Equal(field, error?.Field);
    }

    [Fact]
    public void P3_FR023_Goals_have_at_most_500_characters()
    {
        var sprint = NewSprint();

        Assert.Equal("Goal", sprint.Update("MOB Sprint 1", new string('g', 501))?.Field);
        Assert.Null(sprint.Update("MOB Sprint 1", new string('g', 500)));
    }

    // ----- Starting -----

    [Fact]
    public void P3_US2_AS5_Starting_defaults_the_end_to_two_weeks_later()
    {
        var sprint = NewSprint();

        Assert.Null(sprint.Start(October5, null, "Ship refunds", T0));

        Assert.Equal(SprintState.Active, sprint.State);
        Assert.Equal((October5, new DateOnly(2026, 10, 19)), (sprint.StartDate, sprint.EndDate));
        Assert.Equal("Ship refunds", sprint.Goal);
        Assert.Equal(T0, sprint.StartedAt);
    }

    [Fact]
    public void Starting_keeps_a_chosen_end_and_the_goal_when_none_is_given()
    {
        var sprint = NewSprint();
        Assert.Null(sprint.Update("MOB Sprint 1", "Refunds"));

        Assert.Null(sprint.Start(October5, October5.AddDays(7), null, T0));

        Assert.Equal(October5.AddDays(7), sprint.EndDate);
        Assert.Equal("Refunds", sprint.Goal);
    }

    [Fact]
    public void P3_FR025_The_end_cannot_be_before_the_start()
    {
        var sprint = NewSprint();

        var error = sprint.Start(October5, October5.AddDays(-1), null, T0);

        Assert.Equal((WorkItem.InvalidDatesCode, "EndDate"), (error?.Code, error?.Field));
        Assert.Equal(SprintState.Planned, sprint.State);
    }

    [Fact]
    public void Sprint_dates_lie_in_2000_to_2099()
    {
        Assert.Equal("StartDate", NewSprint().Start(new DateOnly(1999, 12, 31), null, null, T0)?.Field);
        Assert.Equal("EndDate", NewSprint().Start(new DateOnly(2099, 12, 30), null, null, T0)?.Field);
    }

    [Fact]
    public void Only_a_planned_sprint_can_start()
    {
        var active = Active();

        Assert.Equal(Sprint.NotPlannedCode, active.Start(October5, null, null, T0)?.Code);
    }

    // ----- Completing and deleting -----

    [Fact]
    public void P3_US2_AS7_Completing_needs_an_active_sprint_and_then_refuses_every_change()
    {
        Assert.Equal(Sprint.NotActiveCode, NewSprint().Complete(T0)?.Code);

        var sprint = Active();
        Assert.Null(sprint.Complete(T0.AddDays(14)));
        Assert.Equal((SprintState.Completed, T0.AddDays(14)), (sprint.State, sprint.CompletedAt));

        Assert.Equal(Sprint.CompletedCode, sprint.Complete(T0)?.Code);
        Assert.Equal(Sprint.CompletedCode, sprint.Update("New name", null)?.Code);
        Assert.Equal(Sprint.CompletedCode, sprint.Start(October5, null, null, T0)?.Code);
        Assert.Equal(Sprint.CompletedCode, sprint.CheckDeletable()?.Code);
        Assert.True(sprint.Ref.IsCompleted);
    }

    [Fact]
    public void P3_FR023_Only_planned_sprints_can_be_deleted()
    {
        Assert.Null(NewSprint().CheckDeletable());
        Assert.Equal(Sprint.NotPlannedCode, Active().CheckDeletable()?.Code);
    }

    [Fact]
    public void An_active_sprint_past_its_end_date_is_overdue()
    {
        var sprint = Active();

        Assert.False(sprint.IsOverdue(new DateOnly(2026, 10, 19)));
        Assert.True(sprint.IsOverdue(new DateOnly(2026, 10, 20)));
        Assert.False(NewSprint().IsOverdue(new DateOnly(2030, 1, 1)));
    }

    // ----- Cards joining and leaving sprints (FR-024, FR-030) -----

    [Fact]
    public void P3_FR030_A_card_joins_moves_between_and_leaves_sprints_with_history()
    {
        var story = New(WorkItemType.Story);

        Assert.Null(story.JoinSprint(null, Sprint1, At(1)));
        Assert.Null(story.JoinSprint(Sprint1, Sprint2, At(2)));
        Assert.Null(story.JoinSprint(Sprint2, null, At(3)));

        Assert.Null(story.SprintId);
        var sprintChanges = story.Changes.Where(c => c.Field == WorkItemField.Sprint).Select(c => (c.OldValue, c.NewValue));
        Assert.Equal([(null, "MOB Sprint 1"), ("MOB Sprint 1", "MOB Sprint 2"), ("MOB Sprint 2", null)], sprintChanges);
        Assert.Equal(T0.AddMinutes(3), story.UpdatedAt);
    }

    [Fact]
    public void Joining_the_sprint_it_is_in_records_nothing()
    {
        var story = New(WorkItemType.Story);
        Assert.Null(story.JoinSprint(null, Sprint1, At(1)));
        var changes = story.Changes.Count;

        Assert.Null(story.JoinSprint(Sprint1 with { }, Sprint1, At(2)));
        Assert.Equal(changes, story.Changes.Count);
    }

    [Fact]
    public void P3_FR024_Epics_are_never_in_a_sprint()
    {
        Assert.Equal(WorkItem.TypeNotAllowedCode, New(WorkItemType.Epic).JoinSprint(null, Sprint1, At())?.Code);
    }

    [Fact]
    public void P3_US2_AS4_A_sub_task_cannot_join_a_sprint_on_its_own()
    {
        var subtask = New(WorkItemType.Story).AddSubtask(3, "Write the refund form", ToDo, Rank.First(), At()).Value!;

        Assert.Equal(WorkItem.TypeNotAllowedCode, subtask.JoinSprint(null, Sprint1, At())?.Code);
    }

    [Fact]
    public void P3_FR028_Nothing_moves_into_a_completed_sprint_and_done_cards_stay_in_it()
    {
        var open = New(WorkItemType.Bug);
        Assert.Equal(Sprint.CompletedCode, open.JoinSprint(null, Sprint1Completed, At())?.Code);

        var done = New(WorkItemType.Story, Done);
        Assert.Null(done.JoinSprint(null, Sprint1, At(1)));
        Assert.Equal(Sprint.CompletedCode, done.JoinSprint(Sprint1Completed, null, At(2))?.Code);
        Assert.Equal(1, done.SprintId);
    }

    [Fact]
    public void An_item_reopened_after_its_sprint_completed_joins_the_next_sprint_from_the_backlog()
    {
        var story = New(WorkItemType.Story);
        Assert.Null(story.JoinSprint(null, Sprint1, At(1)));

        // Sprint 1 completed while the story was done; it was reopened later, so it is in the backlog.
        Assert.Null(story.JoinSprint(Sprint1Completed, Sprint2, At(2)));

        Assert.Equal(2, story.SprintId);
        Assert.Equal((null, "MOB Sprint 2"), story.Changes.Where(c => c.Field == WorkItemField.Sprint).Select(c => (c.OldValue, c.NewValue)).Last());
    }

    [Fact]
    public void A_scope_event_keeps_the_estimate_of_its_moment()
    {
        var story = New(WorkItemType.Story);
        Assert.Null(story.SetEstimate(5, ProjectTemplate.Scrum, At()));

        var committed = SprintScopeEvent.Create(1, 42, SprintScopeKind.Committed, story.Estimate, At(1));
        Assert.Null(story.SetEstimate(8, ProjectTemplate.Scrum, At(2)));

        Assert.Equal((1L, 42L, SprintScopeKind.Committed, 5m), (committed.SprintId, committed.WorkItemId, committed.Kind, committed.Estimate));
        Assert.Equal((Amina, T0.AddMinutes(1)), (committed.ActorId, committed.OccurredAt));
    }
}
