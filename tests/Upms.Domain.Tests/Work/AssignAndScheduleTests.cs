using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Assignees and start and due dates (Phase 2 FR-016, FR-018, FR-023).</summary>
public sealed class AssignAndScheduleTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly PersonRef Amina = new(Guid.NewGuid(), "Amina Khan");
    private static readonly PersonRef Bilal = new(Guid.NewGuid(), "Bilal Ahmed");
    private static readonly Guid Owen = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    private static ChangeContext By(Guid actor, int minutes = 0) => ChangeContext.New(actor, T0.AddMinutes(minutes));

    private static WorkItem NewTask() =>
        WorkItem.CreateTask(10, "WEB", 1, "Design the home page", ToDo, Rank.First(), By(Owen)).Value!;

    private static (WorkItemField, string?, string?) Summary(WorkItemChange change) => (change.Field, change.OldValue, change.NewValue);

    [Fact]
    public void P2_US2_AS1_Assigning_records_the_old_and_new_assignee_by_name()
    {
        var task = NewTask();

        task.Assign(null, Amina, By(Owen, 5));

        Assert.Equal(Amina.Id, task.AssigneeId);
        Assert.Equal(T0.AddMinutes(5), task.UpdatedAt);
        Assert.Equal((WorkItemField.Assignee, null, "Amina Khan"), Summary(task.Changes.Last()));
        Assert.Equal(Owen, task.Changes.Last().ActorId);
    }

    [Fact]
    public void Reassigning_and_unassigning_are_recorded()
    {
        var task = NewTask();
        task.Assign(null, Amina, By(Owen));

        task.Assign(Amina, Bilal, By(Owen, 1));
        task.Assign(Bilal, null, By(Owen, 2));

        Assert.Null(task.AssigneeId);
        Assert.Equal(
            [(WorkItemField.Assignee, null, "Amina Khan"), (WorkItemField.Assignee, "Amina Khan", "Bilal Ahmed"), (WorkItemField.Assignee, "Bilal Ahmed", null)],
            task.Changes.Skip(1).Select(Summary));
    }

    [Fact]
    public void Assigning_the_same_person_again_changes_nothing()
    {
        var task = NewTask();
        task.Assign(null, null, By(Owen, 9));
        task.Assign(null, Amina, By(Owen, 1));

        task.Assign(Amina, Amina, By(Owen, 9));

        Assert.Equal(2, task.Changes.Count);
        Assert.Equal(T0.AddMinutes(1), task.UpdatedAt);
    }

    [Fact]
    public void Naming_the_wrong_current_assignee_is_a_programming_error()
    {
        var task = NewTask();
        task.Assign(null, Amina, By(Owen));

        Assert.Throws<InvalidOperationException>(() => task.Assign(Bilal, null, By(Owen)));
        Assert.Throws<InvalidOperationException>(() => task.Assign(null, Bilal, By(Owen)));
    }

    [Fact]
    public void P2_US2_AS4_Both_dates_are_saved_in_one_change_set_with_ISO_values()
    {
        var task = NewTask();

        Assert.Null(task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10), By(Owen, 3)));

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)), (task.StartDate, task.DueDate));
        Assert.Equal(T0.AddMinutes(3), task.UpdatedAt);
        var rows = task.Changes.Skip(1).ToList();
        Assert.Equal([(WorkItemField.StartDate, null, "2026-10-01"), (WorkItemField.DueDate, null, "2026-10-10")], rows.Select(Summary));
        Assert.Single(rows.Select(r => r.ChangeSetId).Distinct());
    }

    [Fact]
    public void Either_date_may_be_set_alone()
    {
        var onlyStart = NewTask();
        var onlyDue = NewTask();

        Assert.Null(onlyStart.Schedule(new DateOnly(2026, 10, 1), null, By(Owen)));
        Assert.Null(onlyDue.Schedule(null, new DateOnly(2026, 10, 10), By(Owen)));

        Assert.Equal((new DateOnly(2026, 10, 1), (DateOnly?)null), (onlyStart.StartDate, onlyStart.DueDate));
        Assert.Equal(((DateOnly?)null, new DateOnly(2026, 10, 10)), (onlyDue.StartDate, onlyDue.DueDate));
    }

    [Fact]
    public void Only_the_dates_that_changed_are_recorded()
    {
        var task = NewTask();
        task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10), By(Owen));

        Assert.Null(task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 12), By(Owen, 1)));

        Assert.Equal((WorkItemField.DueDate, "2026-10-10", "2026-10-12"), Summary(task.Changes.Last()));
        Assert.Equal(4, task.Changes.Count);
    }

    [Fact]
    public void Dates_can_be_cleared()
    {
        var task = NewTask();
        task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10), By(Owen));

        Assert.Null(task.Schedule(null, null, By(Owen, 1)));

        Assert.Equal(((DateOnly?)null, (DateOnly?)null), (task.StartDate, task.DueDate));
        Assert.Equal([(WorkItemField.StartDate, "2026-10-01", null), (WorkItemField.DueDate, "2026-10-10", null)],
            task.Changes.Skip(3).Select(Summary));
    }

    [Fact]
    public void Saving_the_same_dates_changes_nothing()
    {
        var task = NewTask();
        task.Schedule(new DateOnly(2026, 10, 1), null, By(Owen, 1));

        Assert.Null(task.Schedule(new DateOnly(2026, 10, 1), null, By(Owen, 9)));

        Assert.Equal(2, task.Changes.Count);
        Assert.Equal(T0.AddMinutes(1), task.UpdatedAt);
    }

    [Fact]
    public void P2_US2_AS4_A_due_date_before_the_start_date_is_refused_and_nothing_changes()
    {
        var task = NewTask();
        task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10), By(Owen));

        var error = task.Schedule(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 11), By(Owen, 1));

        Assert.Equal((WorkItem.InvalidDatesCode, "DueDate"), (error!.Code, error.Field));
        Assert.Equal("The due date cannot be before the start date.", error.Message);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)), (task.StartDate, task.DueDate));
        Assert.Equal(3, task.Changes.Count);
    }

    [Fact]
    public void The_due_date_may_equal_the_start_date()
    {
        var task = NewTask();

        Assert.Null(task.Schedule(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), By(Owen)));
    }

    [Theory]
    [InlineData(1999, 12, 31, "StartDate")]
    [InlineData(2100, 1, 1, "StartDate")]
    [InlineData(1999, 12, 31, "DueDate")]
    [InlineData(2100, 1, 1, "DueDate")]
    public void Dates_outside_2000_to_2099_are_refused(int year, int month, int day, string field)
    {
        var task = NewTask();
        var date = new DateOnly(year, month, day);

        var error = field == "StartDate" ? task.Schedule(date, null, By(Owen)) : task.Schedule(null, date, By(Owen));

        Assert.Equal((WorkItem.InvalidDatesCode, field), (error!.Code, error.Field));
        Assert.Equal("Choose a date between 1 Jan 2000 and 31 Dec 2099.", error.Message);
        Assert.Single(task.Changes);
    }

    [Fact]
    public void The_first_and_last_allowed_days_are_accepted()
    {
        var task = NewTask();

        Assert.Null(task.Schedule(WorkItem.EarliestDate, WorkItem.LatestDate, By(Owen)));

        Assert.Equal((new DateOnly(2000, 1, 1), new DateOnly(2099, 12, 31)), (task.StartDate!.Value, task.DueDate!.Value));
    }
}
