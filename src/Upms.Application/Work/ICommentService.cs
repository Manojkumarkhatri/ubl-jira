using Upms.Application.Common;
using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>Plain-text comments on a task (FR-030).</summary>
public interface ICommentService
{
    /// <summary>Oldest first, 50 at a time.</summary>
    Task<Result<Page<CommentView>>> ListAsync(string workItemKey, PageRequest page, CancellationToken ct);

    Task<Result<CommentView>> AddAsync(string workItemKey, string body, CancellationToken ct);

    /// <summary>Only the author (<c>CommentNotOwned</c>).</summary>
    Task<Result<CommentView>> EditAsync(long commentId, string body, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Only the author; a "comment deleted" placeholder remains.</summary>
    Task<Result> DeleteAsync(long commentId, CancellationToken ct);
}
