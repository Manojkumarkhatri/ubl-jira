using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Creating and moving tasks, with history (FR-024, FR-025, FR-027, FR-031, constitution IV).</summary>
public sealed class WorkItemCreationAndMoveTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly StatusRef InProgress = new(2, "In Progress", StatusCategory.InProgress);
    private static readonly StatusRef Done = new(3, "Done", StatusCategory.Done);
    private static readonly StatusRef Shipped = new(4, "Shipped", StatusCategory.Done);
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    private static ChangeContext At(DateTimeOffset when) => ChangeContext.New(Amina, when);

    private static WorkItem NewTask(string title = "Design the home page", StatusRef? status = null) =>
        WorkItem.CreateTask(10, "WEB", 1, title, status ?? ToDo, Rank.First(), At(T0)).Value!;

    [Fact]
    public void US1_AS5_A_new_task_gets_its_key_defaults_and_a_Created_entry()
    {
        var task = NewTask();

        Assert.Equal("WEB-1", task.Key);
        Assert.Equal(1, task.Number);
        Assert.Equal(WorkItemType.Task, task.Type);
        Assert.Equal(Priority.Medium, task.Priority);
        Assert.Equal(ToDo.Id, task.StatusId);
        Assert.Equal(Amina, task.CreatedById);
        Assert.Equal(T0, task.CreatedAt);
        Assert.Null(task.ResolvedAt);
        var created = Assert.Single(task.Changes);
        Assert.Equal(WorkItemField.Created, created.Field);
        Assert.Equal("To Do", created.NewValue);
        Assert.Equal(Amina, created.ActorId);
    }

    [Fact]
    public void Titles_are_trimmed()
    {
        Assert.Equal("Design", NewTask("   Design  ").Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void A_title_is_required(string title)
    {
        var result = WorkItem.CreateTask(10, "WEB", 1, title, ToDo, Rank.First(), At(T0));

        Assert.Equal("Title", result.Error!.Field);
    }

    [Fact]
    public void A_title_has_at_most_255_characters()
    {
        Assert.True(WorkItem.CreateTask(10, "WEB", 1, new string('t', 255), ToDo, Rank.First(), At(T0)).IsSuccess);
        Assert.Equal("Title", WorkItem.CreateTask(10, "WEB", 1, new string('t', 256), ToDo, Rank.First(), At(T0)).Error!.Field);
    }

    [Fact]
    public void A_task_created_in_a_done_column_is_completed_at_once()
    {
        Assert.Equal(T0, NewTask(status: Done).ResolvedAt);
    }

    [Fact]
    public void US1_AS6_Moving_to_another_column_changes_the_status_and_records_it()
    {
        var task = NewTask();

        task.Move(ToDo, InProgress, Rank.First(), null, null, null, At(T0.AddHours(1)));

        Assert.Equal(InProgress.Id, task.StatusId);
        Assert.Equal(T0.AddHours(1), task.UpdatedAt);
        var change = task.Changes.Last();
        Assert.Equal(WorkItemField.Status, change.Field);
        Assert.Equal(("To Do", "In Progress"), (change.OldValue, change.NewValue));
    }

    [Fact]
    public void Entering_a_done_column_completes_the_task_and_leaving_it_reopens_it()
    {
        var task = NewTask();

        task.Move(ToDo, Done, Rank.First(), null, null, null, At(T0.AddHours(2)));
        Assert.Equal(T0.AddHours(2), task.ResolvedAt);

        task.Move(Done, Shipped, Rank.First(), null, null, null, At(T0.AddHours(3)));
        Assert.Equal(T0.AddHours(2), task.ResolvedAt);

        task.Move(Shipped, InProgress, Rank.First(), null, null, null, At(T0.AddHours(4)));
        Assert.Null(task.ResolvedAt);
    }

    [Fact]
    public void US1_AS7_A_reorder_within_the_column_is_recorded_with_old_and_new_positions()
    {
        var task = NewTask();
        var newRank = Rank.Before(task.Rank);

        task.Move(ToDo, ToDo, newRank, oldPosition: 4, newPosition: 1, note: "moved to top", At(T0.AddMinutes(5)));

        Assert.Equal(newRank, task.Rank);
        Assert.Equal(ToDo.Id, task.StatusId);
        var change = task.Changes.Last();
        Assert.Equal(WorkItemField.Rank, change.Field);
        Assert.Equal(("4", "1", "moved to top"), (change.OldValue, change.NewValue, change.Note));
    }

    [Fact]
    public void A_move_with_a_note_keeps_the_note_in_the_status_entry()
    {
        var task = NewTask();

        task.Move(ToDo, Done, Rank.First(), null, null, "column deleted", At(T0));

        Assert.Equal("column deleted", task.Changes.Last().Note);
    }

    [Fact]
    public void Every_change_of_one_action_shares_a_change_set()
    {
        var context = At(T0);
        var task = WorkItem.CreateTask(10, "WEB", 7, "Plan", ToDo, Rank.First(), context).Value!;

        Assert.All(task.Changes, c => Assert.Equal(context.ChangeSetId, c.ChangeSetId));
    }
}
