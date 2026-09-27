using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>"My tasks": the caller's open assigned tasks across projects (Phase 2 FR-025). owen runs "Website Revamp"
/// (WEB) and "Payroll" (PAY); amina and bilal are Members of both.</summary>
public sealed class MyTasksServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly DateOnly Sep26 = new(2026, 9, 26);
    private static readonly DateOnly Sep28 = new(2026, 9, 28);
    private static readonly DateOnly Oct5 = new(2026, 10, 5);

    private User _owen = null!;
    private User _amina = null!;
    private User _bilal = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _owen = await Data.UserAsync("owen");
        _amina = await Data.UserAsync("amina");
        _bilal = await Data.UserAsync("bilal");
        ActAs(_owen);
        foreach (var (name, key) in new[] { ("Website Revamp", "WEB"), ("Payroll", "PAY") })
        {
            (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync(name, key, null, Ct))).ValueOrThrow();
            await Data.MembersAsync(key, ProjectRole.Member, _amina, _bilal);
        }
    }

    private async Task<string> TaskAsync(string projectKey, string title, User? assignee = null, DateOnly? due = null,
        Priority priority = Priority.Medium)
    {
        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync(projectKey, false, Ct))).ValueOrThrow();
        var card = (await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync(projectKey, board.Columns[0].Id, title, Ct))).ValueOrThrow();
        await ChangeAsync(card.Key, assignee, due, priority);
        return card.Key;
    }

    private async Task<string> SubtaskAsync(string parentKey, string title, User? assignee = null, DateOnly? due = null,
        Priority priority = Priority.Medium)
    {
        var parent = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync(parentKey, title, Ct))).ValueOrThrow();
        var key = parent.Subtasks.Items[^1].Key;
        await ChangeAsync(key, assignee, due, priority);
        return key;
    }

    private async Task ChangeAsync(string key, User? assignee, DateOnly? due, Priority priority)
    {
        foreach (var edit in new WorkItemEdit[] { new WorkItemEdit.Assignee(assignee?.Id), new WorkItemEdit.Dates(null, due), new WorkItemEdit.Priority(priority) })
        {
            await EditAsync(key, edit);
        }
    }

    private async Task EditAsync(string key, WorkItemEdit edit)
    {
        var details = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync(key, Ct))).ValueOrThrow();
        (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.UpdateAsync(key, edit, details.Version, Ct))).ValueOrThrow();
    }

    private async Task<Page<MyTaskRow>> MyTasksAsync(User user, PageRequest? page = null)
    {
        ActAs(user);
        var result = await CallAsync<IMyTasksService, Result<Page<MyTaskRow>>>(s => s.ListAsync(page ?? PageRequest.First, Ct));
        ActAs(_owen);
        return result.ValueOrThrow();
    }

    [Fact]
    public async Task P2_US2_AS7_Open_assigned_tasks_from_every_project_by_project_name_due_date_priority_and_number()
    {
        var web1 = await TaskAsync("WEB", "Design the home page", _amina, Sep26);
        await TaskAsync("WEB", "Write the copy", _bilal, Sep26);
        var web3 = await TaskAsync("WEB", "Pick the colours", _amina, priority: Priority.Low);
        var web4 = await TaskAsync("WEB", "Book the photographer", _amina, Sep28);
        var web5 = await TaskAsync("WEB", "Check the fonts", _amina, Sep28, Priority.Highest);
        var web6 = await SubtaskAsync(web1, "Wireframes", _amina, Sep28);
        var web7 = await TaskAsync("WEB", "Order the logo", _amina, priority: Priority.Highest);
        var pay1 = await TaskAsync("PAY", "Close September", _amina, Oct5);
        for (var i = 0; i < 2; i++)
        {
            await TaskAsync("WEB", $"Filler {i}"); // WEB-8 and WEB-9, unassigned
        }

        var web10 = await TaskAsync("WEB", "Test the forms", _amina, Sep28, Priority.Highest);

        var page = await MyTasksAsync(_amina);

        Assert.Equal([pay1, web1, web5, web10, web4, web6, web7, web3], page.Items.Select(r => r.Key));
        Assert.Equal(8, page.TotalCount);
        var first = page.Items[0];
        Assert.Equal(("PAY", "Payroll", "Close September", Oct5, Priority.Medium),
            (first.ProjectKey, first.ProjectName, first.Title, first.DueDate!.Value, first.Priority));
        Assert.Equal(("To Do", StatusCategory.ToDo), (first.Status.Name, first.Status.Category));
        Assert.Equal(web1, page.Items.Single(r => r.Key == web6).ParentKey);
        Assert.Null(page.Items.Single(r => r.Key == web1).ParentKey);
    }

    [Fact]
    public async Task Completed_deleted_and_other_peoples_tasks_are_left_out()
    {
        var open = await TaskAsync("WEB", "Design the home page", _amina);
        var done = await TaskAsync("WEB", "Write the copy", _amina);
        var deleted = await TaskAsync("WEB", "Pick the colours", _amina);
        await TaskAsync("WEB", "Book the photographer", _bilal);
        var doneColumn = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow().Columns[^1].Id;
        await EditAsync(done, new WorkItemEdit.Status(doneColumn));
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(deleted, Ct))).IsSuccess);

        var page = await MyTasksAsync(_amina);

        Assert.Equal([open], page.Items.Select(r => r.Key));
    }

    [Fact]
    public async Task P2_US2_AS8_Tasks_in_a_project_the_person_was_removed_from_are_left_out()
    {
        await TaskAsync("WEB", "Design the home page", _amina);
        await TaskAsync("PAY", "Close September", _amina);
        var team = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("PAY", Ct))).ValueOrThrow();

        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.RemoveAsync("PAY", _amina.Id, team.MembersVersion, Ct))).ValueOrThrow();
        var page = await MyTasksAsync(_amina);

        Assert.Equal(["WEB-1"], page.Items.Select(r => r.Key));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Administrators_see_their_tasks_in_every_project()
    {
        var ada = await Data.AdministratorAsync("ada");
        await Data.MembersAsync("PAY", ProjectRole.Member, ada);
        var key = await TaskAsync("PAY", "Close September", ada);
        var team = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("PAY", Ct))).ValueOrThrow();
        (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.RemoveAsync("PAY", ada.Id, team.MembersVersion, Ct))).ValueOrThrow();

        var page = await MyTasksAsync(ada);

        Assert.Equal([key], page.Items.Select(r => r.Key));
    }

    [Fact]
    public async Task The_list_is_paged()
    {
        for (var i = 1; i <= 5; i++)
        {
            await TaskAsync("WEB", $"Task {i}", _amina);
        }

        var second = await MyTasksAsync(_amina, new PageRequest(2, 2));
        var last = await MyTasksAsync(_amina, new PageRequest(3, 2));

        Assert.Equal(["WEB-3", "WEB-4"], second.Items.Select(r => r.Key));
        Assert.Equal(["WEB-5"], last.Items.Select(r => r.Key));
        Assert.Equal((5, 3), (last.TotalCount, last.PageCount));
    }

    [Fact]
    public async Task Someone_with_no_assigned_tasks_gets_an_empty_page()
    {
        await TaskAsync("WEB", "Design the home page", _bilal);

        var page = await MyTasksAsync(_amina);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }
}
