using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Editing tasks, sub-tasks, deleting and restoring (FR-026, FR-028, FR-031, FR-033).</summary>
public sealed class WorkItemEditingTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly Guid Bilal = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    private static ChangeContext By(Guid actor, int minutes = 0) => ChangeContext.New(actor, T0.AddMinutes(minutes));

    private static WorkItem NewTask() =>
        WorkItem.CreateTask(10, "WEB", 1, "Design the home page", ToDo, Rank.First(), By(Amina)).Value!;

    private static WorkItemChange Last(WorkItem item) => item.Changes.Last();

    [Fact]
    public void US2_AS2_Renaming_trims_the_title_and_records_old_and_new()
    {
        var task = NewTask();

        Assert.Null(task.Rename("  Design the landing page ", By(Bilal, 5)));

        Assert.Equal("Design the landing page", task.Title);
        Assert.Equal(T0.AddMinutes(5), task.UpdatedAt);
        Assert.Equal((WorkItemField.Title, "Design the home page", "Design the landing page", Bilal),
            (Last(task).Field, Last(task).OldValue, Last(task).NewValue, Last(task).ActorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Renaming_to_an_empty_title_is_refused(string title)
    {
        var task = NewTask();

        Assert.Equal("Title", task.Rename(title, By(Bilal))!.Field);
        Assert.Equal("Design the home page", task.Title);
    }

    [Fact]
    public void Renaming_to_the_same_title_records_nothing()
    {
        var task = NewTask();

        task.Rename("Design the home page", By(Bilal));

        Assert.Single(task.Changes);
    }

    [Fact]
    public void US2_AS3_The_description_keeps_line_breaks_and_is_recorded()
    {
        var task = NewTask();
        const string text = "First line\nSecond line with https://example.com";

        Assert.Null(task.Describe(text, By(Bilal)));

        Assert.Equal(text, task.Description);
        Assert.Equal((WorkItemField.Description, null, text), (Last(task).Field, Last(task).OldValue, Last(task).NewValue));
    }

    [Fact]
    public void The_description_has_at_most_32000_characters_and_blank_clears_it()
    {
        var task = NewTask();

        Assert.Null(task.Describe(new string('d', 32_000), By(Bilal)));
        Assert.Equal("Description", task.Describe(new string('d', 32_001), By(Bilal))!.Field);
        Assert.Null(task.Describe("   ", By(Bilal)));
        Assert.Null(task.Description);
    }

    [Fact]
    public void US2_AS4_Changing_the_priority_is_recorded()
    {
        var task = NewTask();

        task.Prioritize(Priority.High, By(Bilal));

        Assert.Equal(Priority.High, task.Priority);
        Assert.Equal((WorkItemField.Priority, "Medium", "High"), (Last(task).Field, Last(task).OldValue, Last(task).NewValue));
    }

    [Fact]
    public void US2_AS6_A_sub_task_has_its_own_key_and_the_parent_records_it()
    {
        var task = NewTask();

        var subtask = task.AddSubtask(7, "Draw wireframes", ToDo, Rank.First(), By(Bilal)).Value!;

        Assert.Equal("WEB-7", subtask.Key);
        Assert.Equal(WorkItemType.Subtask, subtask.Type);
        Assert.Equal(task.Id, subtask.ParentId);
        Assert.Equal(task.ProjectId, subtask.ProjectId);
        Assert.Equal(WorkItemField.Created, Assert.Single(subtask.Changes).Field);
        Assert.Equal((WorkItemField.SubtaskAdded, "WEB-7"), (Last(task).Field, Last(task).NewValue));
    }

    [Fact]
    public void A_sub_task_cannot_have_sub_tasks()
    {
        var subtask = NewTask().AddSubtask(7, "Draw wireframes", ToDo, Rank.First(), By(Bilal)).Value!;

        var error = subtask.AddSubtask(8, "Too deep", ToDo, Rank.First(), By(Bilal)).Error;

        Assert.Equal("SubtaskDepth", error!.Code);
    }

    [Fact]
    public void US2_AS11_Deleting_a_task_deletes_its_sub_tasks_and_restoring_brings_them_back()
    {
        var task = NewTask();
        var first = task.AddSubtask(2, "One", ToDo, Rank.First(), By(Amina)).Value!;
        var second = task.AddSubtask(3, "Two", ToDo, Rank.After(Rank.First()), By(Amina)).Value!;
        var context = By(Bilal, 10);

        task.Delete([first, second], context);

        Assert.All(new[] { task, first, second }, w =>
        {
            Assert.True(w.IsDeleted);
            Assert.Equal(context.At, w.DeletedAt);
            Assert.Equal(Bilal, w.DeletedById);
            Assert.Equal((WorkItemField.Deleted, context.ChangeSetId), (Last(w).Field, Last(w).ChangeSetId));
        });

        task.Restore([first, second], By(Amina, 20));

        Assert.All(new[] { task, first, second }, w =>
        {
            Assert.False(w.IsDeleted);
            Assert.Null(w.DeletedAt);
            Assert.Equal(WorkItemField.Restored, Last(w).Field);
        });
    }

    [Fact]
    public void Restoring_a_task_leaves_sub_tasks_deleted_earlier_on_their_own()
    {
        var task = NewTask();
        var earlier = task.AddSubtask(2, "Deleted first", ToDo, Rank.First(), By(Amina)).Value!;
        earlier.Delete([], By(Amina, 1));
        var later = task.AddSubtask(3, "Deleted with the task", ToDo, Rank.After(Rank.First()), By(Amina)).Value!;
        task.Delete([later], By(Bilal, 5));

        task.Restore([earlier, later], By(Amina, 10));

        Assert.True(earlier.IsDeleted);
        Assert.False(later.IsDeleted);
    }

    [Fact]
    public void Activity_records_history_without_changing_the_task()
    {
        var task = NewTask();
        var updatedAt = task.UpdatedAt;

        task.RecordActivity(WorkItemField.CommentAdded, null, "Looks good", By(Bilal, 30), "comment 5");

        Assert.Equal(updatedAt, task.UpdatedAt);
        Assert.Equal((WorkItemField.CommentAdded, "Looks good", "comment 5"), (Last(task).Field, Last(task).NewValue, Last(task).Note));
    }

    [Fact]
    public void US2_AS8_Only_the_author_edits_or_deletes_a_comment()
    {
        var comment = Comment.Create(5, Amina, "  First thoughts  ", T0).Value!;
        Assert.Equal("First thoughts", comment.Body);

        Assert.Equal("CommentNotOwned", comment.Edit(Bilal, "Hijacked", T0.AddMinutes(1))!.Code);
        Assert.Equal("CommentNotOwned", comment.Delete(Bilal, T0.AddMinutes(1))!.Code);

        Assert.Null(comment.Edit(Amina, "Second thoughts", T0.AddMinutes(2)));
        Assert.Equal(("Second thoughts", T0.AddMinutes(2)), (comment.Body, comment.EditedAt));

        Assert.Null(comment.Delete(Amina, T0.AddMinutes(3)));
        Assert.True(comment.IsDeleted);
        Assert.Equal("Validation", comment.Edit(Amina, "After deletion", T0.AddMinutes(4))!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_comment_needs_text(string body)
    {
        Assert.Equal("Body", Comment.Create(5, Amina, body, T0).Error!.Field);
    }

    [Fact]
    public void A_comment_has_at_most_32000_characters()
    {
        Assert.True(Comment.Create(5, Amina, new string('c', 32_000), T0).IsSuccess);
        Assert.Equal("Body", Comment.Create(5, Amina, new string('c', 32_001), T0).Error!.Field);
    }
}
