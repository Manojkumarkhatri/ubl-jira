using Upms.Application.Common.Results;
using Upms.Application.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>The drawer's "⋯" actions: no time logged, no votes; the other actions are not used by these tests.</summary>
public sealed class FakeWorkItemActionService : IWorkItemActionService
{
    public Task<Result<TimeTracking>> GetTimeAsync(string workItemKey, CancellationToken ct) =>
        Task.FromResult(Result<TimeTracking>.Ok(new TimeTracking(workItemKey, null, null, 0, [], true)));

    public Task<Result<TimeTracking>> LogWorkAsync(string workItemKey, WorkLogInput input, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<TimeTracking>> ChangeWorkLogAsync(long workLogId, WorkLogInput input, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<TimeTracking>> DeleteWorkLogAsync(long workLogId, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result<TimeTracking>> SetOriginalEstimateAsync(string workItemKey, string? text, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<TimeTracking>> SetRemainingEstimateAsync(string workItemKey, string? text, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<VoteState>> ToggleVoteAsync(string workItemKey, CancellationToken ct) =>
        Task.FromResult(Result<VoteState>.Ok(new VoteState(1, true)));

    public Task<Result> SetCoverAsync(string workItemKey, string? color, long? attachmentId, CancellationToken ct) =>
        Task.FromResult(Result.Ok());

    public Task<Result<string>> CloneAsync(string workItemKey, CloneRequest request, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<MovePreview>> PreviewMoveAsync(string workItemKey, MoveTarget request, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<string>> MoveAsync(string workItemKey, MoveTarget request, CancellationToken ct) =>
        throw new NotSupportedException();
}
