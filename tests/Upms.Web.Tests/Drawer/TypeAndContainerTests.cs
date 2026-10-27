using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's type, epic or stage, estimate and container sections (Phase 3 FR-011, FR-014–FR-016).</summary>
public sealed class TypeAndContainerTests : BunitTestBase
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeWorkItemService _items = new();

    public TypeAndContainerTests()
    {
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        _items.Details = FakeWorkItemService.Sample() with
        {
            Type = WorkItemType.Bug,
            TypeChoices = [WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug],
        };
    }

    private IRenderedComponent<TaskDrawer> Open() => Render<TaskDrawer>(p => p.Add(x => x.WorkItemKey, "WEB-1"));

    private static WorkItemDetails Epic(bool canContribute = true) => FakeWorkItemService.Sample() with
    {
        Type = WorkItemType.Epic,
        Title = "Card refunds",
        CanContribute = canContribute,
        AsContainer = new ContainerSummary(new Progress(1, 2), new Page<ContainedItemView>(
        [
            new ContainedItemView("WEB-2", WorkItemType.Story, "Refund to card", FakeWorkItemService.Done, null, null, [1]),
            new ContainedItemView("WEB-3", WorkItemType.Bug, "Refund shows twice", FakeWorkItemService.ToDo, null, null, [2]),
        ], 2, 1, 50)),
    };

    [Fact]
    public void P3_US1_AS6_The_type_choice_offers_story_task_and_bug_and_saves_the_change()
    {
        var cut = Open();

        Assert.Equal(["Task", "Story", "Bug"], cut.FindAll("#drawer-type option").Select(o => o.TextContent.Trim()));
        Assert.Equal("Bug", cut.Find("#drawer-type").GetAttribute("value"));

        cut.Find("#drawer-type").Change("Story");

        Assert.Equal(WorkItemType.Story, Assert.IsType<WorkItemEdit.Type>(Assert.Single(_items.Edits).Edit).Value);
        Assert.Contains("Type changed to Story", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P3_US1_AS6_An_epic_shows_its_type_without_a_choice()
    {
        _items.Details = Epic();

        var cut = Open();

        Assert.Empty(cut.FindAll("#drawer-type"));
        Assert.Equal("Epic", cut.Find("[data-testid=drawer-type-text]").TextContent.Trim());
    }

    [Fact]
    public void P3_US1_AS5_A_story_goes_into_an_epic_and_out_again()
    {
        var cut = Open();

        Assert.Equal("Epic", cut.Find("label[for=drawer-container]").TextContent.Trim());
        Assert.Equal(["None", "WEB-7 Card refunds", "WEB-8 Statements"], cut.FindAll("#drawer-container option").Select(o => o.TextContent.Trim()));

        cut.Find("#drawer-container").Change("WEB-7");
        Assert.Equal("WEB-7", Assert.IsType<WorkItemEdit.Container>(_items.Edits[^1].Edit).ContainerKey);
        Assert.Equal("WEB-7", cut.Find("#drawer-container").GetAttribute("value"));

        cut.Find("#drawer-container").Change("");
        Assert.Null(Assert.IsType<WorkItemEdit.Container>(_items.Edits[^1].Edit).ContainerKey);
    }

    [Fact]
    public void In_a_Stage_gate_project_the_choice_is_the_stage()
    {
        _items.Details = FakeWorkItemService.Sample() with { Template = ProjectTemplate.StageGate };
        _items.Containers = [new ContainerOption("WEB-1", "Initiation", WorkItemType.Stage, false)];

        var cut = Open();

        Assert.Equal("Stage", cut.Find("label[for=drawer-container]").TextContent.Trim());
        Assert.Empty(cut.FindAll("#drawer-estimate"));
    }

    [Fact]
    public void Epics_stages_and_sub_tasks_have_no_container_choice()
    {
        _items.Details = Epic();

        var cut = Open();

        Assert.Empty(cut.FindAll("#drawer-container"));
    }

    [Fact]
    public void P3_US1_AS7_A_milestone_has_one_date_and_no_sub_tasks()
    {
        _items.Details = FakeWorkItemService.Sample() with { Type = WorkItemType.Milestone, Template = ProjectTemplate.StageGate };

        var cut = Open();

        Assert.Empty(cut.FindAll("#drawer-start"));
        Assert.Equal("Date", cut.Find("label[for=drawer-due]").TextContent.Trim());
        Assert.Empty(cut.FindAll("#add-subtask"));
        Assert.Equal("Milestone", cut.Find("[data-testid=drawer-type-text]").TextContent.Trim());
    }

    [Fact]
    public void A_milestone_s_date_is_saved_as_its_date()
    {
        _items.Details = FakeWorkItemService.Sample() with { Type = WorkItemType.Milestone, Template = ProjectTemplate.StageGate };
        var cut = Open();

        cut.Find("#drawer-due").Change("2026-10-30");

        Assert.Contains("Date saved", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Due date", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P3_US1_AS10_Deleting_an_epic_says_its_items_stay()
    {
        _items.Details = Epic();
        var cut = Open();

        cut.ChooseArchive();

        var confirm = cut.WaitForElement("[data-testid=delete-confirm]");
        Assert.Contains("Its 2 items stay in the space, in no epic.", confirm.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sub-task", confirm.TextContent, StringComparison.Ordinal);
        Assert.Equal("Delete epic", confirm.QuerySelector("button.btn-danger")!.TextContent.Trim());
    }

    [Fact]
    public void P3_FR014_The_estimate_saves_with_one_decimal()
    {
        var cut = Open();

        cut.Find("#drawer-estimate").Change("2.5");

        Assert.Equal(2.5m, Assert.IsType<WorkItemEdit.Estimate>(Assert.Single(_items.Edits).Edit).Value);
        Assert.Contains("Estimate saved", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_estimate_shows_why_and_keeps_what_was_typed()
    {
        _items.NextEditResult = _ => AppError.Validation("Estimate", "An estimate is a number from 0 to 999.9, with at most one decimal place.");
        var cut = Open();

        cut.Find("#drawer-estimate").Change("2.55");

        Assert.Contains("at most one decimal place", cut.Find("#drawer-estimate-error").TextContent, StringComparison.Ordinal);
        Assert.Equal("2.55", cut.Find("#drawer-estimate").GetAttribute("value"));
        Assert.Equal("true", cut.Find("#drawer-estimate").GetAttribute("aria-invalid"));
    }

    [Fact]
    public void An_estimate_that_is_not_a_number_is_not_sent()
    {
        var cut = Open();

        cut.Find("#drawer-estimate").Change("two");

        Assert.Empty(_items.Edits);
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("#drawer-estimate-error").TextContent));
    }

    [Fact]
    public void P3_US1_AS5_An_epic_lists_its_items_with_its_progress()
    {
        _items.Details = Epic();

        var cut = Open();

        var section = cut.Find("[data-testid=container-items]");
        Assert.Contains("1 of 2 done (50%)", section.TextContent, StringComparison.Ordinal);
        Assert.Equal(["WEB-2", "WEB-3"], section.QuerySelectorAll("[data-testid=contained-key]").Select(k => k.TextContent.Trim()));
        Assert.Equal("50", section.QuerySelector("progress")!.GetAttribute("value"));
    }

    [Fact]
    public void P3_FR015_An_item_is_added_to_an_epic_from_its_drawer()
    {
        _items.Details = Epic();
        var cut = Open();

        cut.Find("[data-testid=container-items] .add-child-toggle").Click(); // "Add child work item" opens the box, as in Jira
        cut.Find("#container-add-input").Input("Refund by bank transfer");
        cut.Find("#container-add-input").KeyDown("Enter");

        var (projectKey, item) = Assert.Single(_items.Created);
        Assert.Equal(("WEB", "Refund by bank transfer", "WEB-1"), (projectKey, item.Title, item.ContainerKey));
        Assert.Equal(WorkItemType.Task, item.Type);
    }

    [Fact]
    public void Viewers_read_the_type_epic_estimate_and_items_but_change_nothing()
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Type = WorkItemType.Story,
            TypeChoices = [WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug],
            Container = new ContainerView("WEB-7", "Card refunds", WorkItemType.Epic),
            Estimate = 3,
            CanContribute = false,
        };

        var cut = Open();

        Assert.Empty(cut.FindAll("#drawer-type"));
        Assert.Empty(cut.FindAll("#drawer-container"));
        Assert.Empty(cut.FindAll("#drawer-estimate"));
        Assert.Equal("Story", cut.Find("[data-testid=drawer-type-text]").TextContent.Trim());
        Assert.Contains("WEB-7 Card refunds", cut.Find("[data-testid=drawer-container-text]").TextContent, StringComparison.Ordinal);
        Assert.Equal("3", cut.Find("[data-testid=drawer-estimate-text]").TextContent.Trim());

        _items.Details = Epic(canContribute: false);
        var epic = Open();
        Assert.Empty(epic.FindAll("[data-testid=container-items] .add-child-toggle"));
        Assert.Empty(epic.FindAll("#container-add-input"));
    }

    [Theory]
    [InlineData(WorkItemField.Type, "Bug", "Story", "changed the type from Bug to Story")]
    [InlineData(WorkItemField.Container, null, "WEB-7 Card refunds", "put the bug in WEB-7 Card refunds")]
    [InlineData(WorkItemField.Container, "WEB-7 Card refunds", null, "took the bug out of WEB-7 Card refunds")]
    [InlineData(WorkItemField.Container, "WEB-7 Card refunds", "WEB-8 Statements", "moved the bug from WEB-7 Card refunds to WEB-8 Statements")]
    [InlineData(WorkItemField.Estimate, null, "3", "set the estimate to 3")]
    [InlineData(WorkItemField.Estimate, "3", "2.5", "changed the estimate from 3 to 2.5")]
    [InlineData(WorkItemField.Estimate, "3", null, "cleared the estimate (was 3)")]
    public void Type_container_and_estimate_changes_read_as_sentences_in_the_history(WorkItemField field, string? from, string? to,
        string expected)
    {
        _items.Details = _items.Details with
        {
            History = new Page<ChangeView>([new ChangeView(T0, "Amina Khan", field, from, to, null)], 1, 1, 50),
        };

        var cut = Open().ShowHistory();

        Assert.Contains($"Amina Khan {expected}", cut.Find("[data-testid=history-entry]").TextContent, StringComparison.Ordinal);
    }
}
