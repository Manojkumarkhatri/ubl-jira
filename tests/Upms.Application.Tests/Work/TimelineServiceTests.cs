using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The Timeline view and rescheduling (Phase 2 FR-034–FR-040). WEB is amina's project.</summary>
public sealed class TimelineServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static DateOnly Oct(int day) => new(2026, 10, day);

    private async Task<TimelineView> TimelineAsync(bool hideCompleted = false) =>
        (await CallAsync<ITimelineService, Result<TimelineView>>(s => s.GetAsync("WEB", hideCompleted, Ct))).ValueOrThrow();

    private Task<Result<TimelineItem>> RescheduleAsync(string key, DateOnly? start, DateOnly? due, byte[] version) =>
        CallAsync<ITimelineService, Result<TimelineItem>>(s => s.RescheduleAsync(key, start, due, version, Ct));

    private async Task<TimelineItem> ItemAsync(string key) =>
        (await TimelineAsync()).Rows.SelectMany(r => r.ScheduledSubtasks.Prepend(r.Task)).Single(i => i.Key == key);

    private async Task TaskAsync(string title, DateOnly? start = null, DateOnly? due = null)
    {
        var card = await AddAsync(ToDo, title);
        if (start is not null || due is not null)
        {
            await EditAsync(card.Key, new WorkItemEdit.Dates(start, due));
        }
    }

    private async Task SubtaskAsync(string parentKey, string title, DateOnly? start = null, DateOnly? due = null)
    {
        var parent = await AddSubtaskAsync(parentKey, title);
        if (start is not null || due is not null)
        {
            await EditAsync(parent.Subtasks.Items[^1].Key, new WorkItemEdit.Dates(start, due));
        }
    }

    /// <summary>WEB-1 5–10 Oct, WEB-2 due 3 Oct only, WEB-3 5–8 Oct, WEB-4 undated, WEB-5 undated with a sub-task
    /// WEB-7 from 1 Oct, WEB-6 5–10 Oct, and WEB-8 an undated sub-task of WEB-1.</summary>
    private async Task PlanAsync()
    {
        await TaskAsync("Plan the launch", Oct(5), Oct(10));
        await TaskAsync("Book the venue", due: Oct(3));
        await TaskAsync("Print the flyers", Oct(5), Oct(8));
        await TaskAsync("Someday");
        await TaskAsync("Prepare the demo");
        await TaskAsync("Invite the press", Oct(5), Oct(10));
        await SubtaskAsync("WEB-5", "Record the video", start: Oct(1));
        await SubtaskAsync("WEB-1", "Choose a date");
    }

    [Fact]
    public async Task P2_US4_AS1_Rows_are_scheduled_tasks_by_first_date_due_date_and_key()
    {
        await PlanAsync();

        var timeline = await TimelineAsync();

        Assert.Equal(["WEB-5", "WEB-2", "WEB-3", "WEB-1", "WEB-6"], timeline.Rows.Select(r => r.Task.Key));
        Assert.Equal((5, 5), (timeline.ScheduledCount, timeline.Rows.Count));
        Assert.Equal(["WEB-4"], timeline.Unscheduled.Items.Select(i => i.Key));
        Assert.Equal(("Website Revamp", true, false), (timeline.ProjectName, timeline.CanContribute, timeline.HidingCompleted));
    }

    [Fact]
    public async Task P2_US4_AS2_A_task_with_one_date_is_on_the_timeline()
    {
        await PlanAsync();

        var bookTheVenue = await ItemAsync("WEB-2");

        Assert.Equal(((DateOnly?)null, Oct(3), Oct(3)), (bookTheVenue.StartDate, bookTheVenue.DueDate!.Value, bookTheVenue.FirstDate!.Value));
        Assert.True(bookTheVenue.IsScheduled);
    }

    [Fact]
    public async Task P2_US4_AS7_Sub_tasks_are_split_into_scheduled_and_unscheduled()
    {
        await PlanAsync();

        var rows = (await TimelineAsync()).Rows;

        var demo = rows.Single(r => r.Task.Key == "WEB-5");
        Assert.False(demo.Task.IsScheduled);
        Assert.Equal(["WEB-7"], demo.ScheduledSubtasks.Select(s => s.Key));
        var launch = rows.Single(r => r.Task.Key == "WEB-1");
        Assert.Empty(launch.ScheduledSubtasks);
        Assert.Equal(["WEB-8"], launch.UnscheduledSubtasks.Select(s => s.Key));
    }

    [Fact]
    public async Task Items_carry_their_status_assignee_and_version()
    {
        var bilal = await MemberAsync("bilal");
        await TaskAsync("Plan the launch", Oct(5), Oct(10));
        await EditAsync("WEB-1", new WorkItemEdit.Assignee(bilal.Id));
        await EditAsync("WEB-1", new WorkItemEdit.Status(InProgress));

        var item = await ItemAsync("WEB-1");

        Assert.Equal(("Plan the launch", "In Progress", true), (item.Title, item.Status.Name, item.IsOpen));
        Assert.Equal("Bilal Tester", item.Assignee!.DisplayName);
        Assert.Equal((await RequireDetailsAsync("WEB-1")).Version, item.Version);
    }

    [Fact]
    public async Task Completed_tasks_are_shown_unless_hidden()
    {
        await PlanAsync();
        await TaskAsync("Old undated work");
        await EditAsync("WEB-2", new WorkItemEdit.Status(Done));
        await EditAsync("WEB-9", new WorkItemEdit.Status(Done));

        var shown = await TimelineAsync();
        var hidden = await TimelineAsync(hideCompleted: true);

        Assert.Contains(shown.Rows, r => r.Task.Key == "WEB-2" && !r.Task.IsOpen);
        Assert.Equal(["WEB-4", "WEB-9"], shown.Unscheduled.Items.Select(i => i.Key));
        Assert.DoesNotContain(hidden.Rows, r => r.Task.Key == "WEB-2");
        Assert.Equal(["WEB-4"], hidden.Unscheduled.Items.Select(i => i.Key));
        Assert.True(hidden.HidingCompleted);
    }

    [Fact]
    public async Task P2_US4_AS6_Unscheduled_tasks_come_50_at_a_time()
    {
        for (var i = 1; i <= 55; i++)
        {
            await AddAsync(ToDo, $"Idea {i}");
        }

        var first = (await TimelineAsync()).Unscheduled;
        var second = (await CallAsync<ITimelineService, Result<Page<TimelineItem>>>(s =>
            s.ListUnscheduledAsync("WEB", false, new PageRequest(2), Ct))).ValueOrThrow();

        Assert.Equal((50, 55), (first.Items.Count, first.TotalCount));
        Assert.Equal(["WEB-1", "WEB-2"], first.Items.Take(2).Select(i => i.Key));
        Assert.Equal(["WEB-51", "WEB-52", "WEB-53", "WEB-54", "WEB-55"], second.Items.Select(i => i.Key));
    }

    [Fact]
    public async Task P2_US4_AS3_Moving_a_bar_shifts_both_dates_in_one_change_set()
    {
        await PlanAsync();
        var item = await ItemAsync("WEB-1");

        var moved = await RescheduleAsync("WEB-1", Oct(19), Oct(24), item.Version);

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal((Oct(19), Oct(24)), (moved.Value!.StartDate!.Value, moved.Value.DueDate!.Value));
        var id = await QueryAsync(db => db.WorkItems.Where(w => w.Key == "WEB-1").Select(w => w.Id).SingleAsync(Ct));
        var changes = await QueryAsync(db => db.WorkItemChanges.AsNoTracking()
            .Where(c => c.WorkItemId == id && (c.Field == WorkItemField.StartDate || c.Field == WorkItemField.DueDate))
            .OrderBy(c => c.Id)
            .ToListAsync(Ct));
        var last = changes.TakeLast(2).ToList();
        Assert.Equal([("2026-10-05", "2026-10-19"), ("2026-10-10", "2026-10-24")], last.Select(c => (c.OldValue, c.NewValue)));
        Assert.Single(last.Select(c => c.ChangeSetId).Distinct());
    }

    [Fact]
    public async Task P2_US4_AS4_Dragging_an_end_changes_only_that_date()
    {
        await PlanAsync();
        var item = await ItemAsync("WEB-1");

        var resized = await RescheduleAsync("WEB-1", Oct(5), Oct(13), item.Version);

        Assert.Equal((Oct(5), Oct(13)), (resized.Value!.StartDate!.Value, resized.Value.DueDate!.Value));
        var history = await HistoryAsync("WEB-1");
        Assert.Equal((WorkItemField.DueDate, "2026-10-10", "2026-10-13"),
            (history.Items[^1].Field, history.Items[^1].OldValue, history.Items[^1].NewValue));
        Assert.DoesNotContain(history.Items.Skip(3), c => c.Field == WorkItemField.StartDate);
    }

    [Fact]
    public async Task A_due_date_before_the_start_date_is_refused()
    {
        await PlanAsync();
        var item = await ItemAsync("WEB-1");

        var result = await RescheduleAsync("WEB-1", Oct(11), Oct(10), item.Version);

        Assert.Equal((ErrorKind.Validation, ErrorCodes.InvalidDates), (result.Error?.Kind, result.Error?.Code));
        Assert.Equal((Oct(5), Oct(10)), ((await ItemAsync("WEB-1")).StartDate!.Value, (await ItemAsync("WEB-1")).DueDate!.Value));
    }

    [Fact]
    public async Task P2_US4_AS8_A_bar_changed_by_someone_else_is_not_overwritten()
    {
        await PlanAsync();
        var loaded = await ItemAsync("WEB-1");
        await EditAsync("WEB-1", new WorkItemEdit.Dates(Oct(6), Oct(12)));

        var result = await RescheduleAsync("WEB-1", Oct(19), Oct(24), loaded.Version);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
        var current = Assert.IsType<TimelineItem>(result.Error!.Current);
        Assert.Equal((Oct(6), Oct(12)), (current.StartDate!.Value, current.DueDate!.Value));
        Assert.Equal((Oct(6), Oct(12)), ((await ItemAsync("WEB-1")).StartDate!.Value, (await ItemAsync("WEB-1")).DueDate!.Value));
    }

    [Fact]
    public async Task P2_US4_AS9_Viewers_read_the_timeline_but_cannot_reschedule()
    {
        await PlanAsync();
        var item = await ItemAsync("WEB-1");
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        var timeline = await TimelineAsync();
        var result = await RescheduleAsync("WEB-1", Oct(19), Oct(24), item.Version);

        Assert.False(timeline.CanContribute);
        Assert.Equal(5, timeline.Rows.Count);
        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Non_members_are_told_the_project_does_not_exist()
    {
        ActAs(await Data.UserAsync("nora"));

        var result = await CallAsync<ITimelineService, Result<TimelineView>>(s => s.GetAsync("WEB", false, Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }
}
