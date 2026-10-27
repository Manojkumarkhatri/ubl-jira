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

/// <summary>Labels on work items (Phase 4 FR-042–FR-044, FR-048, research R18): shared across projects in any case,
/// suggested only from projects the caller can see, and recorded in history.</summary>
public sealed class LabelServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _vera = null!;
    private User _paula = null!;

    /// <summary>amina owns WEB (WEB-1, WEB-2) and MOB (MOB-1); vera is a Viewer of WEB; paula owns PAY (PAY-1), which amina
    /// cannot see.</summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _vera = await MemberAsync("vera", ProjectRole.Viewer);
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Mobile App", "MOB", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
        await CreateAsync(WorkItemType.Task, "Plan the launch");
        await CreateAsync(WorkItemType.Task, "Write the copy");
        await CreateAsync(WorkItemType.Bug, "Card declined", projectKey: "MOB");
        _paula = await Data.UserAsync("paula");
        ActAs(_paula);
        Assert.True((await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Payments", "PAY", null, ProjectTemplate.Kanban, Ct))).IsSuccess);
        await CreateAsync(WorkItemType.Task, "Settle refunds", projectKey: "PAY");
        ActAs(Owner);
    }

    private Task<Result<IReadOnlyList<string>>> AddLabelAsync(string key, string label) =>
        CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync(key, label, Ct));

    private Task<Result<IReadOnlyList<string>>> RemoveLabelAsync(string key, string label) =>
        CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.RemoveAsync(key, label, Ct));

    private Task<Result<IReadOnlyList<string>>> SuggestAsync(string projectKey, string term) =>
        CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.SuggestAsync(projectKey, term, Ct));

    [Fact]
    public async Task P4_US4_AS1_A_contributor_adds_a_label_shown_in_the_drawer()
    {
        Assert.Equal(["launch"], (await AddLabelAsync("WEB-1", "launch")).ValueOrThrow());
        Assert.Equal(["design", "launch"], (await AddLabelAsync("WEB-1", " design ")).ValueOrThrow());

        Assert.Equal(["design", "launch"], (await RequireDetailsAsync("WEB-1")).Labels);
        Assert.Equal(["design", "launch"], (await AddLabelAsync("WEB-1", "LAUNCH")).ValueOrThrow()); // already there
    }

    [Fact]
    public async Task P4_US4_AS2_Suggestions_come_from_live_items_in_projects_the_caller_can_see()
    {
        await AddLabelAsync("WEB-1", "launch");
        await AddLabelAsync("WEB-2", "landing-page");
        await AddLabelAsync("MOB-1", "latency");
        ActAs(_paula);
        await AddLabelAsync("PAY-1", "launch-secret");
        ActAs(Owner);
        await AddLabelAsync("WEB-2", "lab-notes");
        await CreateAsync(WorkItemType.Task, "Gone soon");
        await AddLabelAsync("WEB-3", "lapsed");
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-3", Ct))).IsSuccess);

        Assert.Equal(["lab-notes", "landing-page", "latency", "launch"], (await SuggestAsync("MOB", "la")).ValueOrThrow());
        Assert.Equal(["launch"], (await SuggestAsync("MOB", "LAU")).ValueOrThrow());

        ActAs(_paula);
        Assert.Equal(["launch-secret"], (await SuggestAsync("PAY", "lau")).ValueOrThrow());
    }

    [Fact]
    public async Task At_most_10_labels_are_suggested()
    {
        for (var i = 0; i < 12; i++)
        {
            await AddLabelAsync(i < 6 ? "WEB-1" : "WEB-2", $"team-{i:00}");
        }

        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"team-{i:00}"), (await SuggestAsync("WEB", "team")).ValueOrThrow());
    }

    [Fact]
    public async Task P4_US4_AS3_A_label_in_another_case_reuses_the_first_spelling()
    {
        await AddLabelAsync("WEB-1", "launch");

        Assert.Equal(["launch"], (await AddLabelAsync("MOB-1", "Launch")).ValueOrThrow());
        Assert.Equal(1, await QueryAsync(db => db.Labels.CountAsync(Ct)));
    }

    [Fact]
    public async Task P4_US4_AS5_Removing_a_label_is_recorded_in_history()
    {
        await AddLabelAsync("WEB-1", "launch");
        await AddLabelAsync("WEB-1", "design");

        Assert.Equal(["design"], (await RemoveLabelAsync("WEB-1", "LAUNCH")).ValueOrThrow());
        Assert.Equal(["design"], (await RemoveLabelAsync("WEB-1", "launch")).ValueOrThrow()); // already gone

        var history = (await HistoryAsync("WEB-1")).Items;
        Assert.Contains(history, c => c is { Field: WorkItemField.LabelAdded, NewValue: "launch", ActorName: "Amina Tester" });
        Assert.Contains(history, c => c is { Field: WorkItemField.LabelRemoved, OldValue: "launch", NewValue: null });
        Assert.Single(history, c => c.Field == WorkItemField.LabelRemoved);
    }

    [Fact]
    public async Task P4_US4_AS8_Viewers_see_labels_but_cannot_change_them()
    {
        await AddLabelAsync("WEB-1", "launch");
        ActAs(_vera);

        Assert.Equal(["launch"], (await RequireDetailsAsync("WEB-1")).Labels);
        Assert.Equal(ErrorKind.Forbidden, (await AddLabelAsync("WEB-1", "mine")).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await RemoveLabelAsync("WEB-1", "launch")).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await SuggestAsync("WEB", "la")).Error!.Kind);
    }

    [Fact]
    public async Task People_outside_the_project_are_told_it_does_not_exist()
    {
        ActAs(_paula);

        Assert.Equal(ErrorKind.NotFound, (await AddLabelAsync("WEB-1", "launch")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await SuggestAsync("WEB", "la")).Error!.Kind);
    }

    [Theory]
    [InlineData("tech debt")]
    [InlineData("")]
    public async Task Labels_with_spaces_or_empty_are_refused(string label)
    {
        var error = (await AddLabelAsync("WEB-1", label)).Error!;

        Assert.Equal((ErrorCodes.InvalidLabel, "A label can't contain spaces or commas and can have up to 50 characters."), (error.Code, error.Message));
    }

    [Fact]
    public async Task An_item_takes_at_most_20_labels()
    {
        for (var i = 0; i < 20; i++)
        {
            Assert.True((await AddLabelAsync("WEB-1", $"l{i}")).IsSuccess);
        }

        var error = (await AddLabelAsync("WEB-1", "one-more")).Error!;

        Assert.Equal((ErrorCodes.TooManyLabels, "An item can have up to 20 labels."), (error.Code, error.Message));
        Assert.True((await AddLabelAsync("WEB-1", "L0")).IsSuccess); // one it already has is fine
    }
}
