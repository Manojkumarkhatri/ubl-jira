using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Layout;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Layout;

/// <summary>The Create form (Jira parity pass US1; contracts/ui-routes.md "Create dialog").</summary>
public sealed class CreateWorkDialogTests : BunitTestBase
{
    private readonly FakeWorkItemService _items = new();

    public CreateWorkDialogTests()
    {
        Services.AddSingleton<IWorkItemService>(_items);
    }

    private CreateWorkLauncher Launcher => Services.GetRequiredService<CreateWorkLauncher>();

    private IRenderedComponent<CreateWorkDialog> Open(string address = "spaces/WEB/board", string? spaceKey = null)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        var cut = Render<CreateWorkDialog>();
        cut.InvokeAsync(() => Launcher.Open(spaceKey));
        cut.WaitForElement("#create-summary");
        return cut;
    }

    private static string Selected(IRenderedComponent<CreateWorkDialog> cut, string id) => cut.Find(id).GetAttribute("value") ?? "";

    /// <summary>The work type the type button shows.</summary>
    private static string TypeShown(IRenderedComponent<CreateWorkDialog> cut) => cut.Find("#create-type .type-icon .sr-only").TextContent;

    /// <summary>Chooses a type from Jira's work type menu.</summary>
    private static void ChooseType(IRenderedComponent<CreateWorkDialog> cut, WorkItemType type)
    {
        cut.Find("#create-type").Click();
        cut.Find($"#create-type-list [data-type={type}]").Click();
    }

    /// <summary>"Show all fields": status, epic or stage, sprint and estimate.</summary>
    private static void ShowAllFields(IRenderedComponent<CreateWorkDialog> cut) => cut.Find(".create-expand").Click();

    /// <summary>The form's close button (the form has no Cancel, as in Jira).</summary>
    private static void CloseForm(IRenderedComponent<CreateWorkDialog> cut) =>
        cut.Find("[data-testid=create-work] .modal-head button[aria-label=Close]").Click();

    private static void ChooseFirstParent(IRenderedComponent<CreateWorkDialog> cut)
    {
        cut.Find("#create-parent").Focus();
        cut.WaitForElement("#create-parent-options [role=option]").MouseDown();
    }

    [Fact]
    public void JP_US1_AS1_It_opens_on_the_current_space_with_the_focus_on_the_summary()
    {
        var cut = Open("spaces/MOB/backlog");

        Assert.Equal("MOB", Selected(cut, "#create-space"));
        Assert.Equal(["MOB"], CreateWork.SpaceRequests);
        Assert.Equal(["MOB · Mobile App", "WEB · Website Revamp"], cut.FindAll("#create-space option").Select(o => o.TextContent));
        Assert.Equal("Task", TypeShown(cut));
        Assert.Equal("create-summary", JSInterop.VerifyInvoke("upms.focusById").Arguments[0]);
    }

    [Fact]
    public void A_column_s_Create_opens_the_form_in_that_column()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("spaces/WEB/board");
        var cut = Render<CreateWorkDialog>();

        cut.InvokeAsync(() => Launcher.Open("WEB", FakeCreateWorkService.InProgress.Id));
        cut.WaitForElement("#create-summary");
        ShowAllFields(cut);

        Assert.Equal("2", Selected(cut, "#create-status"));
    }

    [Fact]
    public void Away_from_a_space_it_opens_on_the_default_space()
    {
        CreateWork.DefaultKey = "MOB";

        var cut = Open("for-you");

        Assert.Equal("MOB", Selected(cut, "#create-space"));
    }

    [Fact]
    public void JP_US1_AS2_The_fields_follow_the_space_and_the_type()
    {
        var cut = Open();
        ShowAllFields(cut);

        Assert.Empty(cut.FindAll("#create-sprint")); // Kanban
        Assert.NotNull(cut.Find("#create-estimate"));
        Assert.Equal("Epic", cut.Find("label[for=create-parent]").TextContent);

        ChooseType(cut, WorkItemType.Epic);
        Assert.Empty(cut.FindAll("#create-estimate"));
        Assert.Empty(cut.FindAll("#create-parent"));

        cut.Find("#create-space").Change("MOB");
        cut.WaitForElement("#create-status");
        Assert.Equal("Epic", TypeShown(cut));        // a type the new space offers too is kept
        Assert.Empty(cut.FindAll("#create-sprint")); // epics are in no sprint
        ChooseType(cut, WorkItemType.Story);
        Assert.NotNull(cut.Find("#create-sprint"));
        ChooseType(cut, WorkItemType.Subtask);
        Assert.Equal("Parent *", cut.Find("label[for=create-parent]").TextContent);
        Assert.Empty(cut.FindAll("#create-sprint"));
    }

    [Fact]
    public void JP_US1_AS2_A_late_answer_for_an_earlier_space_is_ignored()
    {
        var cut = Open();
        ShowAllFields(cut);
        CreateWork.Hold("MOB");

        cut.Find("#create-space").Change("MOB");
        cut.Find("#create-space").Change("WEB");
        cut.WaitForElement("#create-status");
        cut.InvokeAsync(() => CreateWork.Release("MOB"));

        Assert.Equal("WEB", Selected(cut, "#create-space"));
        Assert.Empty(cut.FindAll("#create-sprint"));
    }

    [Fact]
    public void JP_US1_AS6_Every_value_is_sent_and_the_new_item_is_announced_with_a_link()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(FormattedText.FromPlainText("Check the refund rules.").Json);
        var cut = Open();

        ChooseType(cut, WorkItemType.Story);
        cut.Find("#create-summary").Input("Refund to card");
        cut.Find("#create-assignee").Change(FakeCreateWorkService.Omar.ToString());
        cut.Find("#create-priority").Change("High");
        cut.Find("#create-due").Change("2026-10-23");
        cut.Find("button[aria-controls=create-labels-panel]").Click();
        cut.Find("#create-labels").Input("payments ");
        ShowAllFields(cut);
        cut.Find("#create-status").Change("2");
        cut.Find("#create-estimate").Change("3");
        ChooseFirstParent(cut);
        cut.Find("[data-testid=create-submit]").Click();

        var (space, item) = Assert.Single(_items.Created);
        Assert.Equal("WEB", space);
        Assert.Equal((WorkItemType.Story, "Refund to card", "WEB-1", 2L), (item.Type, item.Title, item.ContainerKey, item.StatusId));
        Assert.Equal((FakeCreateWorkService.Omar, Priority.High, new DateOnly(2026, 10, 23), 3m), (item.AssigneeId, item.Priority, item.DueDate, item.Estimate));
        Assert.Equal(["payments"], item.Labels);
        Assert.Contains("Check the refund rules.", item.Description, StringComparison.Ordinal);
        var toast = cut.Find("[data-testid=created-toast]");
        Assert.Contains("WEB-9 created", toast.TextContent, StringComparison.Ordinal);
        Assert.Equal("spaces/WEB/board?task=WEB-9", toast.QuerySelector("a")!.GetAttribute("href"));
        Assert.Empty(cut.FindAll("#create-summary")); // closed
    }

    [Fact]
    public void JP_US1_AS7_Create_another_keeps_the_space_type_and_parent_and_clears_the_summary()
    {
        var cut = Open();
        ChooseType(cut, WorkItemType.Subtask);
        ChooseFirstParent(cut);
        cut.Find("[data-testid=create-another]").Change(true);
        cut.Find("#create-summary").Input("Call the hotel");

        cut.Find("[data-testid=create-submit]").Click();

        Assert.Equal("WEB-2", Assert.Single(_items.Created).Item.ParentKey);
        Assert.Equal("", cut.Find("#create-summary").GetAttribute("value"));
        Assert.Equal(("WEB", "Sub-task"), (Selected(cut, "#create-space"), TypeShown(cut)));
        Assert.Contains("WEB-2", cut.Find(".parent-chosen").TextContent, StringComparison.Ordinal);
        JSInterop.VerifyInvoke("upms.editor.setContent");
    }

    [Fact]
    public void JP_US1_AS9_Closing_with_typed_text_asks_first()
    {
        var cut = Open();
        cut.Find("#create-summary").Input("Book the venue");

        CloseForm(cut);
        Assert.NotNull(cut.Find("[data-testid=discard-confirm]"));
        cut.Find("[data-testid=keep-editing]").Click();
        Assert.Equal("Book the venue", cut.Find("#create-summary").GetAttribute("value"));

        CloseForm(cut);
        cut.Find("[data-testid=discard]").Click();
        Assert.Empty(cut.FindAll("#create-summary"));
        Assert.Empty(_items.Created);
    }

    [Fact]
    public void An_empty_form_closes_without_asking()
    {
        var cut = Open();

        CloseForm(cut);

        Assert.Empty(cut.FindAll("[data-testid=discard-confirm]"));
        Assert.Empty(cut.FindAll("#create-summary"));
    }

    [Fact]
    public void A_refusal_is_shown_beside_its_field_and_every_value_stays()
    {
        _items.NextCreateResult = _ => AppError.Rule(ErrorCodes.ParentRequired, "Choose the item this sub-task belongs to.");
        var cut = Open();
        ChooseType(cut, WorkItemType.Subtask);
        cut.Find("#create-summary").Input("Call the hotel");

        cut.Find("[data-testid=create-submit]").Click();

        Assert.Equal("Choose the item this sub-task belongs to.", cut.Find("#create-parent-error").TextContent);
        Assert.Equal("Call the hotel", cut.Find("#create-summary").GetAttribute("value"));
    }

    [Fact]
    public void JP_EdgeCase_A_right_lost_meanwhile_is_said_in_words()
    {
        _items.NextCreateResult = _ => AppError.Forbidden("Viewers can see this space but not change it.");
        var cut = Open();
        cut.Find("#create-summary").Input("Book the venue");

        cut.Find("[data-testid=create-submit]").Click();

        Assert.Equal("You can no longer add work to this space.", cut.Find(".create-form .alert-error").TextContent);
        Assert.Equal("Book the venue", cut.Find("#create-summary").GetAttribute("value"));
    }
}
