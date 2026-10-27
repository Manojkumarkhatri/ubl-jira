using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The task details drawer (FR-023, FR-026, FR-032, research R20).</summary>
public sealed class TaskDrawerTests : BunitTestBase
{
    private readonly FakeWorkItemService _items = new();
    private int _closed;
    private int _changed;

    public TaskDrawerTests()
    {
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        Services.AddScoped<LiveAnnouncer>();
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<ViewerToday>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
    }

    private IRenderedComponent<TaskDrawer> Open(string key = "WEB-1") => Render<TaskDrawer>(p => p
        .Add(x => x.WorkItemKey, key)
        .Add(x => x.OnClose, () => _closed++)
        .Add(x => x.OnChanged, () => _changed++));

    [Fact]
    public void US2_AS1_The_drawer_shows_key_title_status_priority_description_sub_tasks_comments_and_history()
    {
        var cut = Open();

        Assert.Contains("WEB-1", cut.Find("[data-testid=drawer-key]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Design the home page", cut.Find("[data-testid=drawer-title]").TextContent.Trim());
        Assert.Equal("1", cut.Find("#drawer-status").GetAttribute("value"));
        Assert.Equal("High", cut.Find("#drawer-priority").GetAttribute("value"));
        Assert.Contains("Hero first.", cut.Find("[data-testid=drawer-description] .plain-text").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("[data-testid=subtask]").Count);
        // The Activity opens on the comments, as Jira's does; "All" adds the history.
        Assert.Single(cut.FindAll("[data-testid=comment]"));
        Assert.Empty(cut.FindAll("[data-testid=history-entry]"));
        cut.ShowActivity("All");
        Assert.Single(cut.FindAll("[data-testid=comment]"));
        Assert.Equal(2, cut.FindAll("[data-testid=history-entry]").Count);
    }

    [Fact]
    public void P4_US1_AS2_A_formatted_description_shows_formatted_and_editing_opens_the_editor()
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Description = Rich.FromDocument("""{"type":"doc","content":[{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Plan"}]}]}"""),
        };
        var cut = Open();

        Assert.Equal("Plan", cut.Find("[data-testid=drawer-description] .formatted-text h5").TextContent);

        cut.Find("[data-testid=edit-description]").Click();

        // The editor starts from the document; its formatting toolbar appears when text is selected (editor.js).
        Assert.NotNull(cut.Find("#drawer-description .rte-content"));
        var create = JSInterop.Invocations.Single(i => i.Identifier == "upms.editor.create").Arguments[1]!;
        Assert.Equal(_items.Details.Description!.Content, create.GetType().GetProperty("content")!.GetValue(create));
        Assert.Equal("Description", create.GetType().GetProperty("label")!.GetValue(create));
    }

    [Fact]
    public void P4_FR012_The_drawer_shows_the_watchers()
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Watchers = new WatchersView(2, true, [new(CurrentUser.UserId!.Value, "Amina Khan", "amina"), new(Guid.NewGuid(), "Omar Ali", "omar")], false),
        };
        var cut = Open();

        // Jira's eye button at the top of the item: the count, and "Stop watching" with the list behind it.
        var count = cut.Find("[data-testid=watchers-count]");
        Assert.Equal("2 watchers, you are watching", count.TextContent.Trim());
        count.Click();
        Assert.Equal("Stop watching", cut.Find("[data-testid=watch-toggle]").TextContent.Trim());
    }

    [Fact]
    public void P2_US1_AS3_A_Viewer_reads_the_drawer_but_is_offered_no_changes()
    {
        var mine = new CommentView(12, CurrentUser.UserId!.Value, "Amina Khan", true, Rich.Plain("My earlier note"), DateTimeOffset.UtcNow, null, false, [7]);
        _items.Details = FakeWorkItemService.Sample() with
        {
            CanContribute = false,
            CanDelete = false,
            Comments = new Upms.Application.Common.Page<CommentView>([mine], 1, 1, 50),
        };

        var cut = Open();

        Assert.Equal("Design the home page", cut.Find("[data-testid=drawer-title]").TextContent.Trim());
        Assert.Empty(cut.FindAll("#drawer-title-button"));
        Assert.Empty(cut.FindAll("[data-testid=edit-description]"));
        Assert.NotNull(cut.Find("#drawer-status").GetAttribute("disabled"));
        Assert.NotNull(cut.Find("#drawer-priority").GetAttribute("disabled"));
        Assert.Empty(cut.FindAll(".add-child-toggle"));
        Assert.Empty(cut.FindAll("#add-subtask"));
        Assert.Empty(cut.FindAll("[data-testid=mark-done]"));
        Assert.All(cut.FindAll("select[id^='subtask-status-']"), select => Assert.NotNull(select.GetAttribute("disabled")));
        Assert.Empty(cut.FindAll("[data-testid=start-comment]"));
        Assert.Empty(cut.FindAll(".comment-actions"));
        Assert.Contains("My earlier note", cut.Find("[data-testid=comment]").TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=item-actions]").Click();
        Assert.Empty(cut.FindAll("[data-testid=item-action-delete]"));
    }

    [Fact]
    public void The_drawer_opens_as_a_modal_dialog_and_the_close_button_closes_it()
    {
        var cut = Open();

        JSInterop.VerifyInvoke("upms.dialog.open"); // showModal: focus moves in and returns to the card on close
        cut.Find("button[aria-label='Close task details']").Click();

        JSInterop.VerifyInvoke("upms.dialog.close");
        Assert.Equal(1, _closed);
    }

    [Fact]
    public void Escape_closes_the_drawer_once()
    {
        var cut = Open();

        // The browser closes a modal dialog on Esc and reports it; a second report changes nothing.
        cut.InvokeAsync(() => cut.Instance.OnDialogClosed());
        cut.InvokeAsync(() => cut.Instance.OnDialogClosed());

        Assert.Equal(1, _closed);
    }

    [Fact]
    public void US2_AS2_A_new_title_is_saved_with_a_visible_confirmation()
    {
        var cut = Open();

        cut.Find("[data-testid=drawer-title]").Click();
        cut.Find("#drawer-title-input").Input("Design the landing page");
        cut.Find("#drawer-title-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var edit = Assert.Single(_items.Edits);
        Assert.Equal(new WorkItemEdit.Title("Design the landing page"), edit.Edit);
        Assert.Equal(new byte[] { 1, 2, 3 }, edit.Version);
        cut.WaitForAssertion(() => Assert.Contains("Title saved", cut.Find("[data-testid=drawer-saved]").TextContent, StringComparison.Ordinal));
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void US2_AS4_Changing_the_priority_saves_it()
    {
        var cut = Open();

        cut.Find("#drawer-priority").Change("Highest");

        Assert.Equal(new WorkItemEdit.Priority(Priority.Highest), Assert.Single(_items.Edits).Edit);
    }

    [Fact]
    public void US2_AS5_Changing_the_status_saves_it_and_shows_warnings()
    {
        _items.NextEditResult = _ => Result<WorkItemDetails>.Ok(_items.Details with { Status = FakeWorkItemService.Done },
            ["WEB-1 is done but 1 sub-task is still open: WEB-3."]);
        var cut = Open();

        cut.Find("#drawer-status").Change("3");

        Assert.Equal(new WorkItemEdit.Status(3), Assert.Single(_items.Edits).Edit);
        cut.WaitForAssertion(() => Assert.Contains("WEB-3", cut.Find("[data-testid=drawer-warning]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_title_keeps_the_typed_text_and_shows_the_error()
    {
        _items.NextEditResult = _ => AppError.Validation("Title", "The title can have at most 255 characters.");
        var cut = Open();
        var tooLong = new string('x', 256);

        cut.Find("[data-testid=drawer-title]").Click();
        cut.Find("#drawer-title-input").Input(tooLong);
        cut.Find("#drawer-title-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(tooLong, cut.Find("#drawer-title-input").GetAttribute("value"));
        Assert.Contains("255", cut.Find("#drawer-title-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US2_AS10_A_conflict_shows_the_latest_version_and_keeps_the_users_text()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Rich.Document("My careful rewrite"));
        var latest = _items.Details with { Description = Rich.Plain("Bilal's version"), Version = [9] };
        _items.NextEditResult = _ => AppError.Conflict("WEB-1 was changed by someone else.", latest);
        var cut = Open();

        cut.Find("[data-testid=edit-description]").Click();
        cut.Find("[data-testid=drawer-description-save]").Click();

        var banner = cut.Find("[data-testid=conflict]");
        Assert.Contains("changed by someone else", banner.TextContent, StringComparison.Ordinal);
        Assert.Contains("Bilal's version", banner.TextContent, StringComparison.Ordinal);
        // The editor stays open with the typed text, and is not reloaded with the latest version.
        Assert.NotNull(cut.Find("#drawer-description .rte-content"));
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "upms.editor.setContent");
    }

    [Fact]
    public void US2_AS11_Deleting_asks_for_confirmation_showing_the_sub_task_count()
    {
        var cut = Open();

        cut.ChooseArchive();
        var confirm = cut.WaitForElement("[data-testid=delete-confirm]");
        Assert.Contains("Its 2 sub-tasks are deleted with it.", confirm.TextContent, StringComparison.Ordinal);
        confirm.QuerySelector("button.btn-danger")!.Click();

        Assert.Equal(["WEB-1"], _items.Deleted);
        Assert.Equal(1, _closed);
    }

    [Fact]
    public void An_unknown_task_shows_not_found_in_the_drawer()
    {
        var cut = Open("WEB-404");

        Assert.Contains("not found", cut.Find("dialog").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void P3_US3_AS2_Work_in_a_locked_stage_says_which_gate_it_waits_for()
    {
        _items.Details = FakeWorkItemService.Sample() with { Template = Upms.Domain.Common.ProjectTemplate.StageGate, LockedUntil = "Initiation" };

        var cut = Open();

        Assert.Equal("This work can start once the Initiation gate is approved.", cut.Find("[data-testid=locked-notice]").TextContent.Trim());
    }

    [Fact]
    public void P3_FR031_A_stage_s_drawer_shows_its_gate_and_links_to_the_Stages_view()
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Template = Upms.Domain.Common.ProjectTemplate.StageGate,
            Type = WorkItemType.Stage,
            Gate = new GateSummary(GateState.AwaitingDecision, 1, 2),
        };

        var cut = Open();

        var gate = cut.Find("[data-testid=drawer-gate]");
        Assert.Contains("Awaiting decision · 1 of 2 approvals", gate.TextContent, StringComparison.Ordinal);
        Assert.Equal("spaces/WEB/stages?stage=WEB-1", gate.QuerySelector("a")!.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid=locked-notice]"));
    }

    [Theory]
    [InlineData(WorkItemField.GateSetup, null, "Approvers: Farah Khan · approvals needed: all · the requester can't approve", null,
        "set up the gate: Approvers: Farah Khan · approvals needed: all · the requester can't approve")]
    [InlineData(WorkItemField.GateRequested, null, "Request 1", null, "requested approval of the gate")]
    [InlineData(WorkItemField.GateWithdrawn, "Request 1", null, null, "withdrew the request for approval")]
    [InlineData(WorkItemField.GateDecision, "Request 1", "Approved", null, "approved the gate")]
    [InlineData(WorkItemField.GateDecision, "Request 1", "Sent back", "The budget is missing.", "sent the gate back: “The budget is missing.”")]
    [InlineData(WorkItemField.GateApproved, null, "Approved", null, "gave the last approval needed: the gate is approved")]
    public void P3_FR040_Gate_events_read_as_sentences_in_the_history(WorkItemField field, string? from, string? to, string? note, string expected)
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Type = WorkItemType.Stage,
            History = new Page<ChangeView>([new ChangeView(DateTimeOffset.UnixEpoch, "Farah Khan", field, from, to, note)], 1, 1, 50),
        };

        var cut = Open().ShowHistory();

        Assert.Contains($"Farah Khan {expected}", cut.Find("[data-testid=history-entry]").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "MOB Sprint 1", "moved the story into MOB Sprint 1")]
    [InlineData("MOB Sprint 1", "MOB Sprint 2", "moved the story from MOB Sprint 1 to MOB Sprint 2")]
    [InlineData("MOB Sprint 2", null, "moved the story from MOB Sprint 2 to the backlog")]
    public void P3_FR030_Sprint_moves_read_as_sentences_in_the_history(string? from, string? to, string expected)
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Type = WorkItemType.Story,
            History = new Page<ChangeView>([new ChangeView(DateTimeOffset.UnixEpoch, "Amina Khan", WorkItemField.Sprint, from, to, null)], 1, 1, 50),
        };

        var cut = Open().ShowHistory();

        Assert.Contains($"Amina Khan {expected}", cut.Find("[data-testid=history-entry]").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SprintState.Active, null)]
    [InlineData(SprintState.Completed, "spaces/WEB/sprints/1")]
    public void P3_FR030_A_Scrum_card_shows_its_sprint_read_only(SprintState state, string? reportLink)
    {
        _items.Details = FakeWorkItemService.Sample() with
        {
            Template = Upms.Domain.Common.ProjectTemplate.Scrum,
            Type = WorkItemType.Story,
            Sprint = new SprintView(1, "MOB Sprint 1", state),
        };

        var cut = Open();

        var sprint = cut.Find("[data-testid=drawer-sprint]");
        Assert.Equal("MOB Sprint 1", sprint.TextContent.Trim());
        Assert.Equal(reportLink, sprint.QuerySelector("a")?.GetAttribute("href"));
        Assert.Empty(cut.FindAll("select#drawer-sprint"));
    }

    [Fact]
    public void P3_FR030_A_Scrum_card_in_no_sprint_is_in_the_backlog_and_Kanban_cards_have_no_sprint()
    {
        _items.Details = FakeWorkItemService.Sample() with { Template = Upms.Domain.Common.ProjectTemplate.Scrum, Type = WorkItemType.Story };
        Assert.Equal("Backlog", Open().Find("[data-testid=drawer-sprint]").TextContent.Trim());

        _items.Details = FakeWorkItemService.Sample();
        Assert.Empty(Open().FindAll("[data-testid=drawer-sprint]"));
    }
}
