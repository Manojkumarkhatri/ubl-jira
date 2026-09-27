using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Comments (FR-030); each action is recorded in the task's history (FR-031).</summary>
internal sealed class CommentService(
    IAppDbContext db,
    IProjectAccess access,
    IUserDirectory users,
    WorkItemReads reads,
    TimeProvider time) : ICommentService
{
    public async Task<Result<Page<CommentView>>> ListAsync(string workItemKey, PageRequest page, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        return allowed.IsSuccess ? await reads.CommentsAsync(item.Id, allowed.Value!.UserId, page, ct) : allowed.Error!;
    }

    public async Task<Result<CommentView>> AddAsync(string workItemKey, string body, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var now = time.GetUtcNow();
        var created = Comment.Create(item.Id, allowed.Value!.UserId, body, now);
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var comment = created.Value!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        item.RecordActivity(WorkItemField.CommentAdded, null, comment.Body, ChangeContext.New(allowed.Value.UserId, now), $"comment {comment.Id}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ViewAsync(comment, allowed.Value.UserId, ct);
    }

    public async Task<Result<CommentView>> EditAsync(long commentId, string body, byte[] expectedVersion, CancellationToken ct)
    {
        var (comment, item, error) = await LoadAsync(commentId, ct);
        if (error is not null)
        {
            return error;
        }

        var allowed = await access.RequireAsync(item!.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (comment!.AuthorId != allowed.Value!.UserId)
        {
            return AppError.Rule(ErrorCodes.CommentNotOwned, "Only the author can edit a comment.");
        }

        if (!comment.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return AppError.Conflict("This comment was changed in another window. Review it before editing again.",
                await ViewAsync(comment, allowed.Value.UserId, ct));
        }

        var now = time.GetUtcNow();
        var before = comment.Body;
        if (comment.Edit(allowed.Value.UserId, body, now) is { } invalid)
        {
            return invalid.ToAppError();
        }

        item.RecordActivity(WorkItemField.CommentEdited, before, comment.Body, ChangeContext.New(allowed.Value.UserId, now), $"comment {comment.Id}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppError.Conflict("This comment was changed in another window. Review it before editing again.");
        }

        return await ViewAsync(comment, allowed.Value.UserId, ct);
    }

    public async Task<Result> DeleteAsync(long commentId, CancellationToken ct)
    {
        var (comment, item, error) = await LoadAsync(commentId, ct);
        if (error is not null)
        {
            return error;
        }

        var allowed = await access.RequireAsync(item!.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var now = time.GetUtcNow();
        var before = comment!.Body;
        if (comment.Delete(allowed.Value!.UserId, now) is { } refused)
        {
            return refused.ToAppError();
        }

        item.RecordActivity(WorkItemField.CommentDeleted, before, null, ChangeContext.New(allowed.Value.UserId, now), $"comment {comment.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<(Comment? Comment, WorkItem? Item, AppError? Error)> LoadAsync(long commentId, CancellationToken ct)
    {
        var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, ct);
        var item = comment is null ? null : await db.WorkItems.SingleOrDefaultAsync(w => w.Id == comment.WorkItemId, ct);
        return comment is null || item is null ? (null, null, AppError.NotFound("comment")) : (comment, item, null);
    }

    private async Task<CommentView> ViewAsync(Comment comment, Guid viewerId, CancellationToken ct) =>
        WorkItemReads.ToView(comment, viewerId, await users.GetAsync([comment.AuthorId], ct));
}
