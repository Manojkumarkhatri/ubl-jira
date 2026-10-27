using Upms.Application.Common;
using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>Comments on a work item (FR-030), in formatted text since Phase 4 (FR-001, FR-007).</summary>
public interface ICommentService
{
    /// <summary>Oldest first, 50 at a time.</summary>
    Task<Result<Page<CommentView>>> ListAsync(string workItemKey, PageRequest page, CancellationToken ct);

    /// <summary>A comment from the editor's JSON document (<c>UnsupportedFormatting</c>, <c>InvalidLink</c>,
    /// <c>TextTooLong</c>). Its author starts watching; watchers hear about it, and mentioned people are told.</summary>
    Task<Result<CommentView>> AddAsync(string workItemKey, string document, CancellationToken ct);

    /// <summary>Only the author (<c>CommentNotOwned</c>); only people mentioned for the first time are told.</summary>
    Task<Result<CommentView>> EditAsync(long commentId, string document, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Only the author; a "comment deleted" placeholder remains. Nobody is notified.</summary>
    Task<Result> DeleteAsync(long commentId, CancellationToken ct);
}
