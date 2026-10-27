using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>CBU's stages for component tests: Initiation (current, awaiting farah's and omar's decision on request 1,
/// farah approved), Planning, Execution and Closure (locked). Records every call; each succeeds unless a test sets the
/// next error.</summary>
public sealed class FakeStageGateService : IStageGateService
{
    public static readonly Guid Farah = Guid.NewGuid();
    public static readonly Guid Omar = Guid.NewGuid();
    public static readonly Guid Musa = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    public StagesView View { get; set; } = Sample();

    public int Loads { get; private set; }

    public List<(string Project, string Title, string? After)> Added { get; } = [];

    public List<(string Key, int Offset)> Moves { get; } = [];

    public List<(string Key, IReadOnlyList<Guid> Approvers, int? Required, bool Exclude)> SetUps { get; } = [];

    public List<string> Requests { get; } = [];

    public List<(string Key, int Number)> Withdrawals { get; } = [];

    public List<(string Key, int Number, GateDecisionKind Decision, string? Note)> Decisions { get; } = [];

    public List<ApproverOption> People { get; set; } =
    [
        new(Farah, "Farah Khan", "farah", false),
        new(Guid.NewGuid(), "Faisal Iqbal", "faisal", true),
    ];

    public List<ContainedItemView> OpenItems { get; set; } = [];

    public AppError? NextError { get; set; }

    public static GateView Gate(GateState state = GateState.NotRequested, GateRequestView? request = null, bool canDecide = false,
        params ApproverView[] approvers) =>
        new(state, approvers, approvers.Length, true, true, request, canDecide, [7]);

    public static StageView Stage(string key, string title, StagePlace place, GateView? gate = null, int done = 0, int total = 0) =>
        new(key, title, place, null, null, new Progress(done, total), gate ?? Gate(), [1, (byte)key[^1]],
            place == StagePlace.Locked ? "Initiation" : null);

    public static StagesView Sample() => new("CBU", "Core Banking Upgrade", CanContribute: true, CanManage: true, CanRestoreDeleted: false,
    [
        Stage("CBU-1", "Initiation", StagePlace.Current,
            Gate(GateState.AwaitingDecision,
                new GateRequestView(1, "Musa Ali", Musa, T0, [new GateDecisionView(Farah, "Farah Khan", GateDecisionKind.Approve, "Charter looks good.", T0.AddHours(1))], null),
                canDecide: true,
                new ApproverView(Farah, "Farah Khan", true), new ApproverView(Omar, "Omar Ali", true)),
            done: 1, total: 2),
        Stage("CBU-2", "Planning", StagePlace.Locked),
        Stage("CBU-3", "Execution", StagePlace.Locked),
        Stage("CBU-4", "Closure", StagePlace.Locked),
    ]);

    public Task<Result<StagesView>> GetAsync(string projectKey, CancellationToken ct)
    {
        Loads++;
        return Task.FromResult(projectKey == View.ProjectKey ? Result<StagesView>.Ok(View) : Result<StagesView>.Fail(AppError.NotFound("stages")));
    }

    public Task<Result<StagesView>> AddStageAsync(string projectKey, string title, string? afterStageKey, CancellationToken ct)
    {
        Added.Add((projectKey, title, afterStageKey));
        return Answer(View);
    }

    public Task<Result<StagesView>> MoveStageAsync(string stageKey, int offset, byte[] expectedVersion, CancellationToken ct)
    {
        Moves.Add((stageKey, offset));
        var stages = View.Stages.ToList();
        var index = stages.FindIndex(s => s.Key == stageKey);
        var stage = stages[index];
        stages.RemoveAt(index);
        stages.Insert(index + offset, stage);
        View = View with { Stages = stages };
        return Answer(View);
    }

    public Task<Result<IReadOnlyList<ApproverOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<ApproverOption>>.Ok(
            People.Where(p => p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList()));

    public Task<Result<StageView>> SetUpGateAsync(string stageKey, IReadOnlyList<Guid> approverIds, int? requiredApprovals,
        bool excludeRequester, byte[] expectedGateVersion, CancellationToken ct)
    {
        SetUps.Add((stageKey, approverIds, requiredApprovals, excludeRequester));
        return Answer(View.Stages.Single(s => s.Key == stageKey));
    }

    public Task<Result<RequestPreview>> PreviewRequestAsync(string stageKey, CancellationToken ct) =>
        Task.FromResult(Result<RequestPreview>.Ok(new RequestPreview(stageKey, OpenItems)));

    public Task<Result<StageView>> RequestApprovalAsync(string stageKey, byte[] expectedGateVersion, CancellationToken ct)
    {
        Requests.Add(stageKey);
        return Answer(View.Stages.Single(s => s.Key == stageKey));
    }

    public Task<Result<StageView>> WithdrawAsync(string stageKey, int requestNumber, CancellationToken ct)
    {
        Withdrawals.Add((stageKey, requestNumber));
        return Answer(View.Stages.Single(s => s.Key == stageKey));
    }

    public Task<Result<StageView>> DecideAsync(string stageKey, int requestNumber, GateDecisionKind decision, string? note,
        CancellationToken ct)
    {
        Decisions.Add((stageKey, requestNumber, decision, note));
        return Answer(View.Stages.Single(s => s.Key == stageKey));
    }

    private Task<Result<T>> Answer<T>(T value)
    {
        var error = NextError;
        NextError = null;
        return Task.FromResult(error is null ? Result<T>.Ok(value) : Result<T>.Fail(error));
    }
}
