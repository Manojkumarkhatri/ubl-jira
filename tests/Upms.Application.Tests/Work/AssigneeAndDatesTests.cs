using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Assignees and start and due dates in the drawer (Phase 2 FR-016–FR-019, FR-023, FR-024). The owner of WEB
/// is amina, its Project Admin.</summary>
public sealed class AssigneeAndDatesTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October10 = new(2026, 10, 10);

    private Task<Result<WorkItemDetails>> AssignAsync(string key, User? user, byte[]? version = null) =>
        EditAsync(key, new WorkItemEdit.Assignee(user?.Id), version);

    private Task<Result<WorkItemDetails>> ScheduleAsync(string key, DateOnly? start, DateOnly? due, byte[]? version = null) =>
        EditAsync(key, new WorkItemEdit.Dates(start, due), version);

    private async Task<CardView> CardAsync(string key) =>
        (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == key);

    private async Task<int> MembersVersionAsync() =>
        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct))).ValueOrThrow().MembersVersion;

    private async Task ChangeRoleAsync(User user, ProjectRole role)
    {
        var version = await MembersVersionAsync();
        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.ChangeRoleAsync("WEB", user.Id, role, version, Ct))).ValueOrThrow();
    }

    private async Task RemoveAsync(User user)
    {
        var version = await MembersVersionAsync();
        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.RemoveAsync("WEB", user.Id, version, Ct))).ValueOrThrow();
    }

    private async Task DeactivateAsync(User user)
    {
        ActAs(await Data.AdministratorAsync());
        var result = await CallAsync<IUserAdminService, Result>(s => s.DeactivateAsync(user.Id, Ct));
        Assert.True(result.IsSuccess, result.Error?.Message);
        ActAs(Owner);
    }

    [Fact]
    public async Task P2_US2_AS1_Choosing_an_assignee_saves_it_on_the_task_the_card_and_the_history()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");

        var saved = await AssignAsync("WEB-1", bilal);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(new AssigneeRef(bilal.Id, "Bilal Tester", "BT", CanWork: true), saved.Value!.Assignee);
        Assert.Equal(saved.Value.Assignee, (await CardAsync("WEB-1")).Assignee);
        var change = saved.Value.History.Items[^1];
        Assert.Equal((WorkItemField.Assignee, null, "Bilal Tester", Owner.DisplayName),
            (change.Field, change.OldValue, change.NewValue, change.ActorName));
    }

    [Fact]
    public async Task Reassigning_and_unassigning_are_recorded_with_names()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");
        await AssignAsync("WEB-1", bilal);

        await AssignAsync("WEB-1", Owner);
        var unassigned = await AssignAsync("WEB-1", null);

        Assert.Null(unassigned.Value!.Assignee);
        Assert.Null((await CardAsync("WEB-1")).Assignee);
        Assert.Equal([(null, "Bilal Tester"), ("Bilal Tester", "Amina Tester"), ("Amina Tester", null)],
            unassigned.Value.History.Items.Where(c => c.Field == WorkItemField.Assignee).Select(c => (c.OldValue, c.NewValue)));
    }

    [Fact]
    public async Task P2_US2_AS2_A_Member_assigns_a_task_to_themselves_in_one_step()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");
        ActAs(bilal);

        var details = await RequireDetailsAsync("WEB-1");
        var me = Assert.Single(details.AssigneeOptions, o => o.IsMe);
        var saved = await AssignAsync("WEB-1", bilal, details.Version);

        Assert.Equal(bilal.Id, me.UserId);
        Assert.Equal(bilal.Id, saved.Value!.Assignee!.UserId);
    }

    [Fact]
    public async Task P2_US2_AS3_Only_active_Project_Admins_and_Members_are_offered_and_accepted()
    {
        var zara = await MemberAsync("zara");
        var bilal = await MemberAsync("bilal", ProjectRole.ProjectAdmin);
        var vera = await MemberAsync("vera", ProjectRole.Viewer);
        var dora = await Data.UserAsync("dora", isActive: false);
        await Data.MembersAsync("WEB", ProjectRole.Member, dora);
        var nora = await Data.UserAsync("nora");
        await AddAsync(ToDo, "Design the home page");

        var details = await RequireDetailsAsync("WEB-1");

        Assert.Equal([("Amina Tester", true), ("Bilal Tester", false), ("Zara Tester", false)],
            details.AssigneeOptions.Select(o => (o.DisplayName, o.IsMe)));
        Assert.Equal([Owner.Id, bilal.Id, zara.Id], details.AssigneeOptions.Select(o => o.UserId));
        foreach (var refused in new[] { vera, dora, nora })
        {
            var result = await AssignAsync("WEB-1", refused);
            Assert.Equal((ErrorKind.RuleViolation, ErrorCodes.NotAssignable), (result.Error?.Kind, result.Error?.Code));
        }

        var after = await RequireDetailsAsync("WEB-1");
        Assert.Null(after.Assignee);
        Assert.Equal(details.Version, after.Version);
    }

    [Fact]
    public async Task P2_US2_AS3_Someone_removed_since_the_drawer_opened_cannot_be_assigned()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");
        var opened = await RequireDetailsAsync("WEB-1");
        Assert.Contains(opened.AssigneeOptions, o => o.UserId == bilal.Id);

        await RemoveAsync(bilal);
        var result = await AssignAsync("WEB-1", bilal, opened.Version);

        Assert.Equal(ErrorCodes.NotAssignable, result.Error?.Code);
        Assert.DoesNotContain((await RequireDetailsAsync("WEB-1")).AssigneeOptions, o => o.UserId == bilal.Id);
    }

    [Fact]
    public async Task P2_US2_AS4_Both_dates_are_saved_and_shown_on_the_card_and_in_the_history()
    {
        await AddAsync(ToDo, "Design the home page");

        var saved = await ScheduleAsync("WEB-1", October1, October10);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal((October1, October10), (saved.Value!.StartDate!.Value, saved.Value.DueDate!.Value));
        Assert.Equal(October10, (await CardAsync("WEB-1")).DueDate);
        Assert.Equal([(WorkItemField.StartDate, null, "2026-10-01"), (WorkItemField.DueDate, null, "2026-10-10")],
            saved.Value.History.Items.Skip(1).Select(c => (c.Field, c.OldValue, c.NewValue)));
    }

    [Fact]
    public async Task P2_US2_AS4_A_due_date_before_the_start_date_is_refused_and_changes_nothing()
    {
        await AddAsync(ToDo, "Design the home page");
        await ScheduleAsync("WEB-1", October1, October10);

        var result = await ScheduleAsync("WEB-1", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 11));

        Assert.Equal((ErrorKind.Validation, ErrorCodes.InvalidDates), (result.Error?.Kind, result.Error?.Code));
        Assert.Equal(["The due date cannot be before the start date."], result.Error!.FieldErrors!["DueDate"]);
        var details = await RequireDetailsAsync("WEB-1");
        Assert.Equal((October1, October10), (details.StartDate!.Value, details.DueDate!.Value));
    }

    [Fact]
    public async Task Dates_can_be_cleared()
    {
        await AddAsync(ToDo, "Design the home page");
        await ScheduleAsync("WEB-1", October1, October10);

        var cleared = await ScheduleAsync("WEB-1", null, null);

        Assert.Equal(((DateOnly?)null, (DateOnly?)null), (cleared.Value!.StartDate, cleared.Value.DueDate));
        Assert.Null((await CardAsync("WEB-1")).DueDate);
    }

    [Fact]
    public async Task A_stale_version_returns_a_conflict_with_the_latest_values()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");
        var opened = await RequireDetailsAsync("WEB-1");
        await EditAsync("WEB-1", new WorkItemEdit.Title("Design the landing page"));

        var assign = await AssignAsync("WEB-1", bilal, opened.Version);
        var schedule = await ScheduleAsync("WEB-1", October1, October10, opened.Version);

        foreach (var result in new[] { assign, schedule })
        {
            Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
            Assert.Equal("Design the landing page", ((WorkItemDetails)result.Error!.Current!).Title);
        }

        var details = await RequireDetailsAsync("WEB-1");
        Assert.Equal((null, (DateOnly?)null), (details.Assignee, details.DueDate));
    }

    [Fact]
    public async Task Viewers_are_offered_no_one_and_cannot_assign_or_schedule()
    {
        var vera = await MemberAsync("vera", ProjectRole.Viewer);
        await AddAsync(ToDo, "Design the home page");
        ActAs(vera);

        var details = await RequireDetailsAsync("WEB-1");
        var assign = await AssignAsync("WEB-1", Owner, details.Version);
        var schedule = await ScheduleAsync("WEB-1", October1, October10, details.Version);

        Assert.Empty(details.AssigneeOptions);
        Assert.Equal((ErrorKind.Forbidden, ErrorKind.Forbidden), (assign.Error?.Kind, schedule.Error?.Kind));
    }

    [Fact]
    public async Task P2_US2_AS8_An_assignee_who_can_no_longer_work_on_the_project_is_still_shown_and_marked()
    {
        var bilal = await MemberAsync("bilal");
        var cara = await MemberAsync("cara");
        var dan = await MemberAsync("dan");
        await AddAsync(ToDo, "Design the home page");
        await AddAsync(ToDo, "Write the copy");
        await AddAsync(ToDo, "Pick the colours");
        await AssignAsync("WEB-1", bilal);
        await AssignAsync("WEB-2", cara);
        await AssignAsync("WEB-3", dan);

        await ChangeRoleAsync(bilal, ProjectRole.Viewer);
        await RemoveAsync(cara);
        await DeactivateAsync(dan);

        var board = await BoardAsync();
        Assert.Equal([("Bilal Tester", false), ("Cara Tester", false), ("Dan Tester", false)],
            board.Columns[0].Cards.Select(c => (c.Assignee!.DisplayName, c.Assignee.CanWork)));
        var details = await RequireDetailsAsync("WEB-1");
        Assert.Equal((bilal.Id, false), (details.Assignee!.UserId, details.Assignee.CanWork));
        Assert.Equal([Owner.Id], details.AssigneeOptions.Select(o => o.UserId));
    }

    [Fact]
    public async Task Sub_tasks_show_their_assignee_and_due_date()
    {
        var bilal = await MemberAsync("bilal");
        await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync("WEB-1", "Wireframes");
        await AssignAsync("WEB-2", bilal);
        await ScheduleAsync("WEB-2", null, October10);

        var subtask = Assert.Single((await RequireDetailsAsync("WEB-1")).Subtasks.Items);

        Assert.Equal(("Bilal Tester", "BT", October10), (subtask.Assignee!.DisplayName, subtask.Assignee.Initials, subtask.DueDate!.Value));
    }

    [Fact]
    public void Initials_come_from_the_first_and_last_words_of_the_name()
    {
        Assert.Equal("AK", AssigneeRef.InitialsOf("Amina Khan"));
        Assert.Equal("MS", AssigneeRef.InitialsOf("  muhammad  ali   shah "));
        Assert.Equal("OW", AssigneeRef.InitialsOf("Owen"));
        Assert.Equal("Q", AssigneeRef.InitialsOf("Q"));
    }
}
