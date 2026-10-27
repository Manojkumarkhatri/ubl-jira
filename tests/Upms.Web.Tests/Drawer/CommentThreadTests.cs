using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>Comments in the drawer (FR-030).</summary>
public sealed class CommentThreadTests : BunitTestBase
{
    private readonly FakeCommentService _comments;
    private int _changed;

    public CommentThreadTests()
    {
        _comments = new FakeCommentService(CurrentUser.UserId!.Value);
        Services.AddSingleton<ICommentService>(_comments);
        Services.AddScoped<ViewerTimeZone>();
        Services.AddScoped<LiveAnnouncer>();
        Services.AddSingleton<IAccountService>(new FakeAccountService());
    }

    private IRenderedComponent<CommentThread> RenderThread()
    {
        _comments.Comments.Add(new CommentView(1, Guid.NewGuid(), "Bilal Ahmed", false, Rich.Plain("First!"), Start, null, false, [1]));
        _comments.Comments.Add(new CommentView(2, CurrentUser.UserId!.Value, "Amina Khan", true, Rich.Plain("Mine, edited"), Start.AddMinutes(1), Start.AddMinutes(2), false, [2]));
        _comments.Comments.Add(new CommentView(3, Guid.NewGuid(), "Carla Diaz", false, null, Start.AddMinutes(3), null, true, [3]));
        return Render<CommentThread>(p => p
            .Add(x => x.WorkItemKey, "WEB-1")
            .Add(x => x.Initial, new Page<CommentView>(_comments.Comments.ToList(), 3, 1, 50))
            .Add(x => x.OnChanged, () => _changed++));
    }

    [Fact]
    public void US2_AS8_Comments_show_author_time_edited_mark_and_deleted_placeholder()
    {
        var cut = RenderThread();

        var items = cut.FindAll("[data-testid=comment]");
        Assert.Equal(3, items.Count);
        Assert.Contains("Bilal Ahmed", items[0].TextContent, StringComparison.Ordinal);
        Assert.NotNull(items[0].QuerySelector("time"));
        Assert.Contains("(edited)", items[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Comment deleted", items[2].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US2_AS8_Only_your_own_comments_offer_edit_and_delete()
    {
        var cut = RenderThread();

        var items = cut.FindAll("[data-testid=comment]");
        Assert.Empty(items[0].QuerySelectorAll("button"));
        Assert.Equal(["Edit", "Delete"], items[1].QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public void P4_US1_AS1_Posting_a_comment_sends_the_editors_document_and_empties_the_editor()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Rich.Document("Ship it"));
        var cut = RenderThread();

        cut.Find("[data-testid=start-comment]").Click();
        cut.Find("[data-testid=new-comment-save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("[data-testid=comment]").Count));
        Assert.Contains("Ship it", cut.FindAll("[data-testid=comment]")[3].TextContent, StringComparison.Ordinal);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.editor.setContent");
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void A_refused_comment_keeps_the_text_and_says_why()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Rich.Document("Something long"));
        _comments.NextAddResult = _ => AppError.Validation("Body", "The text can have at most 32,000 characters.");
        var cut = RenderThread();

        cut.Find("[data-testid=start-comment]").Click();
        cut.Find("[data-testid=new-comment-save]").Click();

        cut.WaitForAssertion(() => Assert.Contains("32,000", cut.Find("#new-comment-error").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "upms.editor.setContent");
        Assert.Equal(3, cut.FindAll("[data-testid=comment]").Count);
    }

    [Fact]
    public void Editing_your_comment_saves_the_new_text()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Rich.Document("Better wording"));
        var cut = RenderThread();

        cut.FindAll("[data-testid=comment]")[1].QuerySelectorAll("button")[0].Click();
        cut.Find("[data-testid=edit-comment-2-save]").Click();

        cut.WaitForAssertion(() => Assert.Contains("Better wording", cut.FindAll("[data-testid=comment]")[1].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Viewers_see_formatted_comments_without_a_comment_box()
    {
        _comments.Comments.Add(new CommentView(4, Guid.NewGuid(), "Omar Ali", false,
            Rich.FromDocument("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Ready","marks":[{"type":"bold"}]}]}]}"""),
            Start, null, false, [4]));
        var cut = Render<CommentThread>(p => p
            .Add(x => x.WorkItemKey, "WEB-1")
            .Add(x => x.Initial, new Page<CommentView>(_comments.Comments.ToList(), 1, 1, 50))
            .Add(x => x.ReadOnly, true));

        Assert.Equal("Ready", cut.Find("[data-testid=comment] .formatted-text strong").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=start-comment]"));
    }
}
