using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>WEB as amina's Stage-gate project with the stages WEB-1 Initiation, WEB-2 Planning, WEB-3 Execution and WEB-4
/// Closure; omar and musa are Members, vera a Viewer, and farah (the sponsor) is not on the team (Phase 3 User Story
/// 3).</summary>
public abstract class StageGateTestBase(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected const string Initiation = "WEB-1";
    protected const string Planning = "WEB-2";
    protected const string Execution = "WEB-3";
    protected const string Closure = "WEB-4";

    protected override ProjectTemplate Template => ProjectTemplate.StageGate;

    protected User Omar { get; private set; } = null!;

    protected User Musa { get; private set; } = null!;

    protected User Vera { get; private set; } = null!;

    protected User Farah { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Omar = await MemberAsync("omar");
        Musa = await MemberAsync("musa");
        Vera = await MemberAsync("vera", ProjectRole.Viewer);
        Farah = await Data.UserAsync("farah");
    }

    protected Task<Result<StagesView>> TryStagesAsync(string projectKey = "WEB") =>
        CallAsync<IStageGateService, Result<StagesView>>(s => s.GetAsync(projectKey, Ct));

    protected async Task<StagesView> StagesAsync() => (await TryStagesAsync()).ValueOrThrow();

    protected async Task<StageView> StageAsync(string key) => (await StagesAsync()).Stages.Single(s => s.Key == key);

    protected async Task<Result<StageView>> TrySetUpAsync(string stageKey, IEnumerable<User> approvers, int? required = null,
        bool exclude = true)
    {
        var version = (await StageAsync(stageKey)).Gate.Version;
        return await CallAsync<IStageGateService, Result<StageView>>(s =>
            s.SetUpGateAsync(stageKey, approvers.Select(a => a.Id).ToList(), required, exclude, version, Ct));
    }

    /// <summary>Sets up the gate as amina, the Project Admin, then acts as <paramref name="then"/> again.</summary>
    protected async Task<StageView> SetUpAsync(string stageKey, IEnumerable<User> approvers, int? required = null, bool exclude = true,
        User? then = null)
    {
        ActAs(Owner);
        var view = (await TrySetUpAsync(stageKey, approvers, required, exclude)).ValueOrThrow();
        ActAs(then ?? Owner);
        return view;
    }

    protected async Task<Result<StageView>> TryRequestAsync(string stageKey)
    {
        var version = (await StageAsync(stageKey)).Gate.Version;
        return await CallAsync<IStageGateService, Result<StageView>>(s => s.RequestApprovalAsync(stageKey, version, Ct));
    }

    /// <summary>Requests approval as <paramref name="requester"/>, then acts as amina again.</summary>
    protected async Task<StageView> RequestAsync(string stageKey, User requester)
    {
        ActAs(requester);
        var view = (await TryRequestAsync(stageKey)).ValueOrThrow();
        ActAs(Owner);
        return view;
    }

    protected Task<Result<StageView>> TryDecideAsync(string stageKey, int number, GateDecisionKind decision, string? note = null) =>
        CallAsync<IStageGateService, Result<StageView>>(s => s.DecideAsync(stageKey, number, decision, note, Ct));

    /// <summary>Decides as <paramref name="approver"/>, then acts as amina again.</summary>
    protected async Task<StageView> DecideAsync(User approver, string stageKey, int number, GateDecisionKind decision, string? note = null)
    {
        ActAs(approver);
        var view = (await TryDecideAsync(stageKey, number, decision, note)).ValueOrThrow();
        ActAs(Owner);
        return view;
    }

    protected Task<Result<StageView>> TryWithdrawAsync(string stageKey, int number) =>
        CallAsync<IStageGateService, Result<StageView>>(s => s.WithdrawAsync(stageKey, number, Ct));

    /// <summary>Approves the stage's gate with omar alone.</summary>
    protected async Task ApproveAsync(string stageKey)
    {
        await SetUpAsync(stageKey, [Omar], exclude: false);
        var request = await RequestAsync(stageKey, Musa);
        await DecideAsync(Omar, stageKey, request.Gate.CurrentRequest!.Number, GateDecisionKind.Approve);
    }

    /// <summary>A task in the stage, through the list's create box.</summary>
    protected async Task<string> TaskInAsync(string stageKey, string title) =>
        (await CreateAsync(WorkItemType.Task, title, stageKey)).Key;

    protected async Task<Result<WorkItemDetails>> StartAsync(string key) => await EditAsync(key, new WorkItemEdit.Status(InProgress));
}
