using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's assignee and dates (Phase 2 FR-017–FR-021, FR-042). "Today" is 27 Sep 2026 in UTC.</summary>
public sealed class AssigneeAndDatesTests : BunitTestBase
{
    private static readonly Guid Bilal = Guid.NewGuid();
    private readonly FakeWorkItemService _items = new();
    private readonly FakeAccountService _accounts = new();

    public AssigneeAndDatesTests()
    {
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<ViewerToday>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(_accounts);
        _items.Details = FakeWorkItemService.Sample() with
        {
            AssigneeOptions =
            [
                new AssigneeOption(CurrentUser.UserId!.Value, "Amina Khan", IsMe: true),
                new AssigneeOption(Bilal, "Bilal Ahmed", IsMe: false),
            ],
        };
    }

    private IRenderedComponent<TaskDrawer> Open() => Render<TaskDrawer>(p => p.Add(x => x.WorkItemKey, "WEB-1"));

    [Fact]
    public void The_assignee_choice_offers_Unassigned_and_the_people_who_can_be_assigned()
    {
        var cut = Open();

        var options = cut.FindAll("#drawer-assignee option");
        Assert.Equal(["Unassigned", "Amina Khan (me)", "Bilal Ahmed"], options.Select(o => o.TextContent.Trim()));
        Assert.Equal("", cut.Find("#drawer-assignee").GetAttribute("value"));
    }

    [Fact]
    public void P2_US2_AS1_Choosing_an_assignee_saves_it()
    {
        var cut = Open();

        cut.Find("#drawer-assignee").Change(Bilal.ToString());

        var edit = Assert.IsType<WorkItemEdit.Assignee>(Assert.Single(_items.Edits).Edit);
        Assert.Equal(Bilal, edit.UserId);
        Assert.Contains("Assigned to Bilal Ahmed", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
        Assert.Equal(Bilal.ToString(), cut.Find("#drawer-assignee").GetAttribute("value"));
    }

    [Fact]
    public void P2_US2_AS2_Assign_to_me_is_one_step_and_disappears_once_done()
    {
        var cut = Open();

        cut.Find("[data-testid=assign-to-me]").Click();

        var edit = Assert.IsType<WorkItemEdit.Assignee>(Assert.Single(_items.Edits).Edit);
        Assert.Equal(CurrentUser.UserId, edit.UserId);
        Assert.Empty(cut.FindAll("[data-testid=assign-to-me]"));
    }

    [Fact]
    public void Assign_to_me_is_not_offered_to_someone_who_cannot_be_assigned()
    {
        _items.Details = _items.Details with { AssigneeOptions = [new AssigneeOption(Bilal, "Bilal Ahmed", IsMe: false)] };

        var cut = Open();

        Assert.Empty(cut.FindAll("[data-testid=assign-to-me]"));
    }

    [Fact]
    public void An_assignee_who_can_no_longer_work_on_the_project_is_shown_as_such()
    {
        var gone = Guid.NewGuid();
        _items.Details = _items.Details with { Assignee = AssigneeRef.Of(gone, "Gone Away", canWork: false) };

        var cut = Open();

        Assert.Equal(gone.ToString(), cut.Find("#drawer-assignee").GetAttribute("value"));
        var current = cut.Find($"#drawer-assignee option[value='{gone}']");
        Assert.Equal("Gone Away (no longer on the project)", current.TextContent.Trim());
        Assert.True(current.HasAttribute("disabled"));
    }

    [Fact]
    public void A_refused_assignee_reloads_the_choices()
    {
        _items.NextEditResult = _ => AppError.Rule(ErrorCodes.NotAssignable, "That person can't be assigned.");
        var cut = Open();
        var loads = _items.Loads;

        cut.Find("#drawer-assignee").Change(Bilal.ToString());

        Assert.Contains("That person can't be assigned.", cut.Find(".alert-error").TextContent, StringComparison.Ordinal);
        Assert.Equal(loads + 1, _items.Loads);
        Assert.Equal("", cut.Find("#drawer-assignee").GetAttribute("value"));
    }

    [Fact]
    public void P2_US2_AS4_Start_and_due_dates_are_saved_together()
    {
        _items.Details = _items.Details with { StartDate = new DateOnly(2026, 10, 1) };
        var cut = Open();
        Assert.Equal("2026-10-01", cut.Find("#drawer-start").GetAttribute("value"));

        cut.Find("#drawer-due").Change("2026-10-10");

        var edit = Assert.IsType<WorkItemEdit.Dates>(Assert.Single(_items.Edits).Edit);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)), (edit.Start!.Value, edit.Due!.Value));
        Assert.Equal("2026-10-10", cut.Find("#drawer-due").GetAttribute("value"));
        Assert.Contains("Due date saved", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_a_date_sends_no_date()
    {
        _items.Details = _items.Details with { StartDate = new DateOnly(2026, 10, 1), DueDate = new DateOnly(2026, 10, 10) };
        var cut = Open();

        cut.Find("#drawer-start").Change("");

        var edit = Assert.IsType<WorkItemEdit.Dates>(Assert.Single(_items.Edits).Edit);
        Assert.Equal(((DateOnly?)null, new DateOnly(2026, 10, 10)), (edit.Start, edit.Due));
    }

    [Fact]
    public void P2_US2_AS4_Invalid_dates_show_the_message_and_keep_what_was_entered()
    {
        _items.Details = _items.Details with { StartDate = new DateOnly(2026, 10, 12) };
        _items.NextEditResult = _ => new AppError(ErrorKind.Validation, ErrorCodes.InvalidDates, "The due date cannot be before the start date.",
            new Dictionary<string, string[]> { ["DueDate"] = ["The due date cannot be before the start date."] });
        var cut = Open();

        cut.Find("#drawer-due").Change("2026-10-11");

        Assert.Equal("The due date cannot be before the start date.", cut.Find("#drawer-dates-error").TextContent.Trim());
        Assert.Equal("2026-10-11", cut.Find("#drawer-due").GetAttribute("value"));
        Assert.Equal("true", cut.Find("#drawer-due").GetAttribute("aria-invalid"));
    }

    [Fact]
    public void P2_US2_AS5_An_open_task_due_before_today_is_marked_Overdue_in_words()
    {
        _items.Details = _items.Details with { DueDate = new DateOnly(2026, 9, 26) };

        var cut = Open();

        Assert.Equal("Overdue", cut.Find("[data-testid=drawer-dates] .overdue-label").TextContent.Trim());
    }

    [Fact]
    public void P2_US2_AS5_A_done_task_or_one_due_today_is_not_overdue()
    {
        _items.Details = _items.Details with { DueDate = new DateOnly(2026, 9, 26), Status = FakeWorkItemService.Done };
        var done = Open();
        Assert.Empty(done.FindAll(".overdue-label"));

        _items.Details = _items.Details with { DueDate = new DateOnly(2026, 9, 27), Status = FakeWorkItemService.ToDo };
        var dueToday = Render<TaskDrawer>(p => p.Add(x => x.WorkItemKey, "WEB-1"));
        Assert.Empty(dueToday.FindAll(".overdue-label"));
    }

    [Fact]
    public void Viewers_see_the_assignee_and_dates_without_controls()
    {
        _items.Details = _items.Details with
        {
            CanContribute = false,
            AssigneeOptions = [],
            Assignee = AssigneeRef.Of(Bilal, "Bilal Ahmed", canWork: true),
            DueDate = new DateOnly(2026, 10, 10),
        };

        var cut = Open();

        Assert.Empty(cut.FindAll("#drawer-assignee"));
        Assert.Empty(cut.FindAll("#drawer-due"));
        Assert.Empty(cut.FindAll("[data-testid=assign-to-me]"));
        Assert.Contains("Bilal Ahmed", cut.Find("[data-testid=drawer-assignee-text]").TextContent, StringComparison.Ordinal);
        Assert.Contains("10 Oct 2026", cut.Find("[data-testid=drawer-dates]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_sub_task_list_shows_each_assignee_and_due_date()
    {
        var subtasks = _items.Details.Subtasks.Items;
        _items.Details = _items.Details with
        {
            Subtasks = new Page<SubtaskView>(
            [
                subtasks[0],
                subtasks[1] with { Assignee = AssigneeRef.Of(Bilal, "Bilal Ahmed", canWork: true), DueDate = new DateOnly(2026, 9, 20) },
            ], 2, 1, 50),
        };

        var cut = Open();

        var second = cut.FindAll("[data-testid=subtask]")[1];
        Assert.Equal("BA", second.QuerySelector(".avatar")!.TextContent.Trim());
        Assert.Contains("Bilal Ahmed", second.TextContent, StringComparison.Ordinal);
        Assert.Contains("20 Sep", second.QuerySelector("[data-testid=due-date]")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Overdue", second.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_history_describes_assignee_and_date_changes()
    {
        var t0 = new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);
        _items.Details = _items.Details with
        {
            History = new Page<ChangeView>(
            [
                new ChangeView(t0, "Owen Tester", WorkItemField.Assignee, null, "Amina Khan", null),
                new ChangeView(t0, "Owen Tester", WorkItemField.Assignee, "Amina Khan", "Bilal Ahmed", null),
                new ChangeView(t0, "Owen Tester", WorkItemField.Assignee, "Bilal Ahmed", null, null),
                new ChangeView(t0, "Owen Tester", WorkItemField.StartDate, null, "2026-10-01", null),
                new ChangeView(t0, "Owen Tester", WorkItemField.DueDate, "2026-10-10", "2026-10-12", null),
                new ChangeView(t0, "Owen Tester", WorkItemField.DueDate, "2026-10-12", null, null),
            ], 6, 1, 50),
        };

        var cut = Open();

        Assert.Equal(
        [
            "Owen Tester assigned the task to Amina Khan",
            "Owen Tester reassigned the task from Amina Khan to Bilal Ahmed",
            "Owen Tester removed the assignee Bilal Ahmed",
            "Owen Tester set the start date to 1 Oct 2026",
            "Owen Tester changed the due date from 10 Oct 2026 to 12 Oct 2026",
            "Owen Tester cleared the due date (was 12 Oct 2026)",
        ], cut.FindAll("[data-testid=history-entry]").Select(e => e.TextContent.Split('·')[0].Trim()));
    }

    [Fact]
    public async Task Today_is_the_calendar_date_in_the_viewers_time_zone()
    {
        Time.SetUtcNow(new DateTimeOffset(2026, 9, 27, 20, 0, 0, TimeSpan.Zero));
        var utc = new ViewerToday(new ViewerTimeZone(new FakeAccountService()), Time);
        var karachi = new ViewerToday(new ViewerTimeZone(new FakeAccountService { TimeZoneId = "Asia/Karachi" }), Time);

        Assert.Equal(new DateOnly(2026, 9, 27), await utc.GetAsync());
        Assert.Equal(new DateOnly(2026, 9, 28), await karachi.GetAsync());
    }
}
