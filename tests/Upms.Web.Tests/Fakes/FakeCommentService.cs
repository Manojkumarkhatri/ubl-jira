using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeCommentService(Guid me) : ICommentService
{
    private long _nextId = 100;

    public List<CommentView> Comments { get; } = [];

    public Func<string, Result<CommentView>>? NextAddResult { get; set; }

    public Task<Result<Page<CommentView>>> ListAsync(string workItemKey, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<CommentView>>.Ok(new Page<CommentView>(Comments.ToList(), Comments.Count, 1, 50)));

    public Task<Result<CommentView>> AddAsync(string workItemKey, string body, CancellationToken ct)
    {
        if (NextAddResult is { } next)
        {
            return Task.FromResult(next(body));
        }

        var comment = new CommentView(_nextId++, me, "Amina Khan", true, body.Trim(), DateTimeOffset.UtcNow, null, false, [1]);
        Comments.Add(comment);
        return Task.FromResult(Result<CommentView>.Ok(comment));
    }

    public Task<Result<CommentView>> EditAsync(long commentId, string body, byte[] expectedVersion, CancellationToken ct)
    {
        var index = Comments.FindIndex(c => c.Id == commentId);
        Comments[index] = Comments[index] with { Body = body, EditedAt = DateTimeOffset.UtcNow };
        return Task.FromResult(Result<CommentView>.Ok(Comments[index]));
    }

    public Task<Result> DeleteAsync(long commentId, CancellationToken ct)
    {
        var index = Comments.FindIndex(c => c.Id == commentId);
        Comments[index] = Comments[index] with { Body = null, IsDeleted = true };
        return Task.FromResult(Result.Ok());
    }
}
