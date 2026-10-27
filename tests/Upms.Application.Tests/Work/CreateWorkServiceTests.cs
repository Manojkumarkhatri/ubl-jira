using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>What the Create form offers (Jira parity pass FR-002–FR-005; research R10). Amina owns WEB (Kanban); she is a
/// Member of MOB (Scrum) and CBU (Stage-gate) and a Viewer of DOC; PAY is not hers at all.</summary>
public sealed class CreateWorkServiceTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    private User _sara = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _sara = await Data.UserAsync("sara");
        ActAs(_sara);
        await SpaceAsync("Mobile App", "MOB", ProjectTemplate.Scrum);
        await SpaceAsync("Core Banking Upgrade", "CBU", ProjectTemplate.StageGate);
        await SpaceAsync("Documentation", "DOC", ProjectTemplate.Kanban);
        await SpaceAsync("Payments", "PAY", ProjectTemplate.Kanban);
        await Data.MembersAsync("MOB", ProjectRole.Member, Owner);
        await Data.MembersAsync("CBU", ProjectRole.Member, Owner);
        await Data.MembersAsync("DOC", ProjectRole.Viewer, Owner);
        ActAs(Owner);
    }

    private async Task SpaceAsync(string name, string key, ProjectTemplate template) =>
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync(name, key, null, template, Ct))).ValueOrThrow();

    private async Task<CreateSpaces> SpacesAsync(string? current = null) =>
        (await CallAsync<ICreateWorkService, Result<CreateSpaces>>(s => s.SpacesAsync(current, Ct))).ValueOrThrow();

    private Task<Result<CreateOptions>> TryOptionsAsync(string key) =>
        CallAsync<ICreateWorkService, Result<CreateOptions>>(s => s.OptionsAsync(key, Ct));

    private async Task<IReadOnlyList<ParentOption>> ParentsAsync(string key, WorkItemType type, string text = "") =>
        (await CallAsync<ICreateWorkService, Result<IReadOnlyList<ParentOption>>>(s => s.FindParentsAsync(key, type, text, Ct))).ValueOrThrow();

    [Fact]
    public async Task JP_FR002_Only_spaces_one_can_contribute_to_are_offered_by_name_with_their_types()
    {
        var spaces = await SpacesAsync();

        Assert.Equal(["Core Banking Upgrade", "Mobile App", "Website Revamp"], spaces.Spaces.Select(s => s.Name));
        Assert.Equal(
            [WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask],
            spaces.Spaces.Single(s => s.Key == "WEB").Types);
        // A Member of a Stage-gate space is not offered stages (FR-003).
        Assert.Equal([WorkItemType.Task, WorkItemType.Milestone, WorkItemType.Subtask], spaces.Spaces.Single(s => s.Key == "CBU").Types);
    }

    [Fact]
    public async Task JP_FR003_Turned_off_types_are_not_offered_and_space_admins_are_offered_stages()
    {
        await QueryAsync(db => db.Database.ExecuteSqlAsync($"UPDATE Projects SET WorkTypes = {(int)(WorkTypeSet.Story | WorkTypeSet.Task)} WHERE [Key] = 'WEB'", Ct));
        ActAs(_sara);

        var spaces = await SpacesAsync();

        Assert.Equal([WorkItemType.Stage, WorkItemType.Task, WorkItemType.Milestone, WorkItemType.Subtask], spaces.Spaces.Single(s => s.Key == "CBU").Types);
        ActAs(Owner);
        Assert.Equal([WorkItemType.Story, WorkItemType.Task, WorkItemType.Subtask], (await SpacesAsync()).Spaces.Single(s => s.Key == "WEB").Types);
    }

    [Fact]
    public async Task JP_US1_AS1_The_default_is_the_current_space_then_the_last_one_created_in_then_the_first_by_name()
    {
        Assert.Equal("CBU", (await SpacesAsync()).DefaultKey);
        Assert.Equal("WEB", (await SpacesAsync("WEB")).DefaultKey);
        Assert.Equal("CBU", (await SpacesAsync("DOC")).DefaultKey); // a Viewer's space is not offered

        await CreateAsync(WorkItemType.Task, "Release notes", projectKey: "MOB");
        Assert.Equal("MOB", (await SpacesAsync()).DefaultKey);
        Assert.Equal("WEB", (await SpacesAsync("WEB")).DefaultKey);
    }

    [Fact]
    public async Task JP_FR004_Options_list_the_columns_people_epics_and_open_sprints()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds", projectKey: "MOB");
        ActAs(_sara);
        var planned = (await CallAsync<ISprintService, Result<SprintSection>>(s => s.CreateAsync("MOB", Ct))).ValueOrThrow();
        ActAs(Owner);

        var options = (await TryOptionsAsync("MOB")).ValueOrThrow();

        Assert.Equal(ProjectTemplate.Scrum, options.Template);
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], options.Columns.Select(c => c.Name));
        Assert.Equal(new[] { Owner.Id, _sara.Id }.Order(), options.Assignees.Select(a => a.UserId).Order());
        Assert.True(options.Assignees.Single(a => a.UserId == Owner.Id).IsMe);
        Assert.Equal([("MOB-1", "Card refunds")], options.Containers.Select(c => (c.Key, c.Title)));
        Assert.Equal([(planned.Id, planned.Name)], options.Sprints.Select(s => (s.Id, s.Name)));
    }

    [Fact]
    public async Task JP_FR005_Parents_are_epics_for_standard_items_and_cards_for_sub_tasks()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");          // WEB-1
        await CreateAsync(WorkItemType.Story, "Refund to card");      // WEB-2
        await CreateAsync(WorkItemType.Bug, "Refund shows twice");    // WEB-3
        await CreateAsync(WorkItemType.Task, "Book the venue");       // WEB-4
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-4", Ct))).IsSuccess);

        Assert.Equal(["WEB-1"], (await ParentsAsync("WEB", WorkItemType.Story)).Select(p => p.Key));
        Assert.Equal(["WEB-2", "WEB-3"], (await ParentsAsync("WEB", WorkItemType.Subtask)).Select(p => p.Key));
        Assert.Equal(["WEB-3"], (await ParentsAsync("WEB", WorkItemType.Subtask, "twice")).Select(p => p.Key));
        Assert.Equal(["WEB-2"], (await ParentsAsync("WEB", WorkItemType.Subtask, "web-2")).Select(p => p.Key));
        Assert.Empty(await ParentsAsync("WEB", WorkItemType.Epic));
    }

    [Fact]
    public async Task At_most_20_parents_are_offered()
    {
        for (var i = 1; i <= 25; i++)
        {
            await AddAsync(ToDo, $"Task {i}");
        }

        Assert.Equal(20, (await ParentsAsync("WEB", WorkItemType.Subtask)).Count);
    }

    [Fact]
    public async Task Spaces_one_cannot_see_are_not_found_and_viewers_are_refused()
    {
        Assert.Equal(ErrorKind.NotFound, (await TryOptionsAsync("PAY")).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await TryOptionsAsync("DOC")).Error!.Kind);
    }
}
