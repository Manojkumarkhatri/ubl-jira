using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Comments (FR-030).</summary>
public sealed class CommentServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await AddAsync(ToDo, "Design the home page");
    }

    private Task<Result<CommentView>> CommentAsync(string body) =>
        CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", body, Ct));

    private async Task<Page<CommentView>> ListAsync(PageRequest? page = null) =>
        (await CallAsync<ICommentService, Result<Page<CommentView>>>(s => s.ListAsync("WEB-1", page ?? PageRequest.First, Ct))).ValueOrThrow();

    [Fact]
    public async Task US2_AS8_Comments_are_listed_oldest_first_with_author_and_time()
    {
        await CommentAsync("First");
        Harness.Time.Advance(TimeSpan.FromMinutes(5));
        ActAs(await Data.UserAsync("bilal"));
        await CommentAsync("Second");

        var comments = (await ListAsync()).Items;

        Assert.Equal(["First", "Second"], comments.Select(c => c.Body));
        Assert.Equal(Owner.DisplayName, comments[0].AuthorName);
        Assert.False(comments[0].IsMine);
        Assert.True(comments[1].IsMine);
        Assert.Equal(Harness.Time.GetUtcNow(), comments[1].CreatedAt);
    }

    [Fact]
    public async Task US2_AS8_Authors_edit_their_comments_which_are_then_marked_as_edited()
    {
        var comment = (await CommentAsync("First draft")).Value!;
        Harness.Time.Advance(TimeSpan.FromMinutes(2));

        var edited = await CallAsync<ICommentService, Result<CommentView>>(s => s.EditAsync(comment.Id, "Final", comment.Version, Ct));

        Assert.True(edited.IsSuccess, edited.Error?.Message);
        Assert.Equal(("Final", Harness.Time.GetUtcNow()), (edited.Value!.Body, edited.Value.EditedAt));
    }

    [Fact]
    public async Task US2_AS8_Authors_delete_their_comments_leaving_a_placeholder()
    {
        var comment = (await CommentAsync("Oops")).Value!;

        var deleted = await CallAsync<ICommentService, Result>(s => s.DeleteAsync(comment.Id, Ct));

        Assert.True(deleted.IsSuccess);
        var placeholder = Assert.Single((await ListAsync()).Items);
        Assert.True(placeholder.IsDeleted);
        Assert.Null(placeholder.Body);
    }

    [Fact]
    public async Task US2_AS8_No_one_edits_or_deletes_someone_elses_comment()
    {
        var comment = (await CommentAsync("Mine")).Value!;
        ActAs(await Data.AdministratorAsync());

        var edit = await CallAsync<ICommentService, Result<CommentView>>(s => s.EditAsync(comment.Id, "Changed", comment.Version, Ct));
        var delete = await CallAsync<ICommentService, Result>(s => s.DeleteAsync(comment.Id, Ct));

        Assert.Equal(ErrorCodes.CommentNotOwned, edit.Error!.Code);
        Assert.Equal(ErrorCodes.CommentNotOwned, delete.Error!.Code);
        Assert.Equal("Mine", (await ListAsync()).Items.Single().Body);
    }

    [Fact]
    public async Task Comment_bodies_must_have_1_to_32000_characters()
    {
        Assert.Equal(ErrorKind.Validation, (await CommentAsync("   ")).Error!.Kind);
        Assert.Equal(ErrorKind.Validation, (await CommentAsync(new string('c', 32_001))).Error!.Kind);
        Assert.True((await CommentAsync(new string('c', 32_000))).IsSuccess);
    }

    [Fact]
    public async Task Comment_activity_is_recorded_in_the_task_history()
    {
        var comment = (await CommentAsync("Hello")).Value!;
        var edited = (await CallAsync<ICommentService, Result<CommentView>>(s => s.EditAsync(comment.Id, "Hello!", comment.Version, Ct))).Value!;
        await CallAsync<ICommentService, Result>(s => s.DeleteAsync(edited.Id, Ct));

        var fields = (await HistoryAsync("WEB-1")).Items.Select(h => h.Field).ToList();

        Assert.Equal([WorkItemField.Created, WorkItemField.CommentAdded, WorkItemField.CommentEdited, WorkItemField.CommentDeleted], fields);
    }

    [Fact]
    public async Task Comments_are_listed_50_at_a_time()
    {
        for (var i = 1; i <= 51; i++)
        {
            await CommentAsync($"Comment {i}");
        }

        var first = await ListAsync();
        var second = await ListAsync(new PageRequest(2));

        Assert.Equal((50, 51), (first.Items.Count, first.TotalCount));
        Assert.Equal("Comment 51", Assert.Single(second.Items).Body);
    }

    [Fact]
    public async Task Authors_names_stay_after_they_are_deactivated()
    {
        var bilal = await Data.UserAsync("bilal");
        ActAs(bilal);
        await CommentAsync("Before I left");
        await QueryAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == bilal.Id, Ct);
            user.Deactivate(Harness.Time.GetUtcNow());
            return await db.SaveChangesAsync(Ct);
        });

        ActAs(Owner);
        Assert.Equal(bilal.DisplayName, (await ListAsync()).Items.Single().AuthorName);
        Assert.Contains((await HistoryAsync("WEB-1")).Items, h => h.ActorName == bilal.DisplayName);
    }
}
