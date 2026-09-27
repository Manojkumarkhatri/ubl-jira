using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The details drawer (FR-025 to FR-027, FR-029, FR-032).</summary>
public sealed class WorkItemServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    [Fact]
    public async Task US2_AS1_Details_include_everything_the_drawer_shows()
    {
        await AddAsync(ToDo, "Design the home page");

        var details = await RequireDetailsAsync("WEB-1");

        Assert.Equal(("WEB-1", "WEB", "Website Revamp", "Design the home page"), (details.Key, details.ProjectKey, details.ProjectName, details.Title));
        Assert.Equal(WorkItemType.Task, details.Type);
        Assert.Equal(("To Do", StatusCategory.ToDo), (details.Status.Name, details.Status.Category));
        Assert.Equal(["To Do", "In Progress", "Done"], details.Statuses.Select(s => s.Name));
        Assert.Equal(Priority.Medium, details.Priority);
        Assert.Null(details.Description);
        Assert.Null(details.Parent);
        Assert.Equal(Owner.DisplayName, details.CreatorName);
        Assert.True(details.CanDelete);
        Assert.Empty(details.Subtasks.Items);
        Assert.Empty(details.Comments.Items);
        Assert.Equal(WorkItemField.Created, Assert.Single(details.History.Items).Field);
    }

    [Fact]
    public async Task US2_AS2_A_new_title_is_saved_and_shown_on_the_board()
    {
        await AddAsync(ToDo, "Design the home page");

        var saved = await EditAsync("WEB-1", new WorkItemEdit.Title("Design the landing page"));

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal("Design the landing page", saved.Value!.Title);
        Assert.Equal("Design the landing page", (await BoardAsync()).Columns[0].Cards.Single().Title);
    }

    [Fact]
    public async Task US2_AS3_A_multi_line_description_keeps_its_line_breaks()
    {
        await AddAsync(ToDo, "Design the home page");
        const string text = "Hero section first.\nSee https://example.com/brief for the brief.";

        var saved = await EditAsync("WEB-1", new WorkItemEdit.Description(text));

        Assert.Equal(text, saved.Value!.Description);
        Assert.Equal(text, (await RequireDetailsAsync("WEB-1")).Description);
    }

    [Fact]
    public async Task US2_AS4_A_new_priority_shows_on_the_card()
    {
        await AddAsync(ToDo, "Design the home page");

        await EditAsync("WEB-1", new WorkItemEdit.Priority(Priority.High));

        Assert.Equal(Priority.High, (await BoardAsync()).Columns[0].Cards.Single().Priority);
    }

    [Fact]
    public async Task US2_AS5_Changing_the_status_to_Done_moves_the_card_and_warns_about_open_sub_tasks()
    {
        await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync("WEB-1", "Wireframes");
        await AddSubtaskAsync("WEB-1", "Mock-ups");

        var saved = await EditAsync("WEB-1", new WorkItemEdit.Status(Done));

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal("Done", saved.Value!.Status.Name);
        Assert.NotNull(saved.Value.ResolvedAt);
        Assert.Equal(["WEB-1"], await KeysInAsync(Done));
        var warning = Assert.Single(saved.Warnings);
        Assert.Contains("WEB-2", warning, StringComparison.Ordinal);
        Assert.Contains("WEB-3", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_invalid_values_is_refused_without_changes()
    {
        await AddAsync(ToDo, "Design the home page");

        var result = await EditAsync("WEB-1", new WorkItemEdit.Title(new string('x', 256)));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Design the home page", (await RequireDetailsAsync("WEB-1")).Title);
    }

    [Fact]
    public async Task US2_AS10_A_save_after_someone_else_changed_the_task_is_a_conflict_with_the_latest_values()
    {
        await AddAsync(ToDo, "Design the home page");
        var loaded = await RequireDetailsAsync("WEB-1");
        ActAs(await Data.UserAsync("bilal"));
        await EditAsync("WEB-1", new WorkItemEdit.Description("Bilal's version"), loaded.Version);

        ActAs(Owner);
        var mine = await EditAsync("WEB-1", new WorkItemEdit.Description("Amina's version"), loaded.Version);

        Assert.Equal(ErrorKind.Conflict, mine.Error!.Kind);
        Assert.Equal("Bilal's version", Assert.IsType<WorkItemDetails>(mine.Error.Current).Description);
        Assert.Equal("Bilal's version", (await RequireDetailsAsync("WEB-1")).Description);
    }

    [Fact]
    public async Task Comments_do_not_cause_conflicts_for_people_editing_the_task()
    {
        await AddAsync(ToDo, "Design the home page");
        var loaded = await RequireDetailsAsync("WEB-1");
        await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", "Looks good", Ct));

        var saved = await EditAsync("WEB-1", new WorkItemEdit.Title("Design the landing page"), loaded.Version);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
    }

    [Fact]
    public async Task Unknown_keys_are_not_found()
    {
        Assert.Equal(ErrorKind.NotFound, (await DetailsAsync("WEB-404")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.UpdateAsync("WEB-404", new WorkItemEdit.Title("x"), [1], Ct))).Error!.Kind);
    }
}
