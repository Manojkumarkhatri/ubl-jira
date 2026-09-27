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
        _comments.Comments.Add(new CommentView(1, Guid.NewGuid(), "Bilal Ahmed", false, "First!", Start, null, false, [1]));
        _comments.Comments.Add(new CommentView(2, CurrentUser.UserId!.Value, "Amina Khan", true, "Mine, edited", Start.AddMinutes(1), Start.AddMinutes(2), false, [2]));
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
    public void Posting_a_comment_adds_it_and_clears_the_box()
    {
        var cut = RenderThread();

        cut.Find("#new-comment").Input("Ship it");
        cut.Find("[data-testid=post-comment]").Click();

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("[data-testid=comment]").Count));
        Assert.Equal("", cut.Find("#new-comment").GetAttribute("value") ?? cut.Find("#new-comment").TextContent);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void A_refused_comment_keeps_the_text()
    {
        _comments.NextAddResult = _ => AppError.Validation("Body", "A comment can have at most 32,000 characters.");
        var cut = RenderThread();

        cut.Find("#new-comment").Input("Something long");
        cut.Find("[data-testid=post-comment]").Click();

        Assert.Equal("Something long", cut.Find("#new-comment").GetAttribute("value") ?? cut.Find("#new-comment").TextContent);
        Assert.Contains("32,000", cut.Find("#new-comment-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_your_comment_saves_the_new_text()
    {
        var cut = RenderThread();

        cut.FindAll("[data-testid=comment]")[1].QuerySelectorAll("button")[0].Click();
        cut.Find("#edit-comment-2").Input("Better wording");
        cut.Find("[data-testid=save-comment]").Click();

        cut.WaitForAssertion(() => Assert.Contains("Better wording", cut.FindAll("[data-testid=comment]")[1].TextContent, StringComparison.Ordinal));
    }
}
