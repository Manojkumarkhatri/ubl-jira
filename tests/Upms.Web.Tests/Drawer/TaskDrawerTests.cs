using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.Single(cut.FindAll("[data-testid=comment]"));
        Assert.Equal(2, cut.FindAll("[data-testid=history-entry]").Count);
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
        var latest = _items.Details with { Description = "Bilal's version", Version = [9] };
        _items.NextEditResult = _ => AppError.Conflict("WEB-1 was changed by someone else.", latest);
        var cut = Open();

        cut.Find("[data-testid=edit-description]").Click();
        cut.Find("#drawer-description-input").Input("My careful rewrite");
        cut.Find("[data-testid=save-description]").Click();

        var banner = cut.Find("[data-testid=conflict]");
        Assert.Contains("changed by someone else", banner.TextContent, StringComparison.Ordinal);
        Assert.Contains("Bilal's version", banner.TextContent, StringComparison.Ordinal);
        Assert.Equal("My careful rewrite", cut.Find("#drawer-description-input").GetAttribute("value") ?? cut.Find("#drawer-description-input").TextContent);
    }

    [Fact]
    public void US2_AS11_Deleting_asks_for_confirmation_showing_the_sub_task_count()
    {
        var cut = Open();

        cut.Find("[data-testid=delete-task]").Click();
        var confirm = cut.WaitForElement("[data-testid=delete-confirm]");
        Assert.Contains("2 sub-tasks", confirm.TextContent, StringComparison.Ordinal);
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
}
