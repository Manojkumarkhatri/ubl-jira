using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Stages;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Stages;

/// <summary>The Stages view of a Stage-gate project (Phase 3 FR-031–FR-040; contracts/ui-routes.md).</summary>
public sealed class StagesPageTests : BunitTestBase
{
    private readonly FakeStageGateService _stages = new();
    private readonly List<string> _announced = [];
    private readonly NavigationManager _navigation;

    public StagesPageTests()
    {
        Services.AddSingleton<IStageGateService>(_stages);
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private IRenderedComponent<StagesPage> RenderStages(string query = "")
    {
        _navigation.NavigateTo($"spaces/CBU/stages{query}");
        return Render<StagesPage>(p => p.Add(x => x.Key, "CBU"));
    }

    private static IElement Stage(IRenderedComponent<StagesPage> cut, string key) => cut.Find($"[data-testid=stage][data-key='{key}']");

    private static string Text(IElement stage, string testId) => stage.QuerySelector($"[data-testid={testId}]")!.TextContent.Trim();

    private static void Click(IRenderedComponent<StagesPage> cut, string dialog, string button) =>
        cut.FindAll($"[data-testid={dialog}] button").Single(b => b.TextContent.Trim() == button).Click();

    private static StageView NotRequested(StageView stage) => stage with
    {
        Gate = FakeStageGateService.Gate(GateState.NotRequested, null, false,
            new ApproverView(FakeStageGateService.Farah, "Farah Khan", true), new ApproverView(FakeStageGateService.Omar, "Omar Ali", true)),
    };

    [Fact]
    public void P3_US3_AS1_The_stages_come_in_order_with_their_place_progress_and_gate()
    {
        var cut = RenderStages();

        Assert.Equal(["Initiation", "Planning", "Execution", "Closure"], cut.FindAll("[data-testid=stage-title]").Select(t => t.TextContent.Trim()));
        var initiation = Stage(cut, "CBU-1");
        Assert.Equal("Current", Text(initiation, "stage-place"));
        Assert.Equal("1 of 2 done (50%)", Text(initiation, "stage-progress"));
        Assert.Equal("Awaiting decision", Text(initiation, "gate-state"));
        Assert.Equal("1 of 2 approvals", Text(initiation, "gate-needed"));
        Assert.Equal(["Farah Khan · Approved", "Omar Ali · Waiting"],
            initiation.QuerySelectorAll("[data-testid=gate-approver]").Select(a => a.TextContent.Trim()));
        Assert.Contains("Requested by Musa Ali", Text(initiation, "gate-request"), StringComparison.Ordinal);
        Assert.Equal("The person who requests approval can't approve.", Text(initiation, "gate-exclusion"));
        Assert.Contains("Charter looks good.", initiation.TextContent, StringComparison.Ordinal);

        var planning = Stage(cut, "CBU-2");
        Assert.Equal("Locked", Text(planning, "stage-place"));
        Assert.Equal("Waits for the Initiation gate", Text(planning, "stage-waits"));
        Assert.Equal("Not requested", Text(planning, "gate-state"));
        Assert.Equal("page", cut.Find("nav.project-nav a[href='spaces/CBU/stages']").GetAttribute("aria-current"));
    }

    [Fact]
    public void P3_US3_AS1_Project_Admins_set_up_a_gate_with_people_from_anywhere()
    {
        var cut = RenderStages();

        Assert.Null(Stage(cut, "CBU-1").QuerySelector("[data-testid=set-up-gate]")); // awaiting a decision
        Stage(cut, "CBU-2").QuerySelector("[data-testid=set-up-gate]")!.Click();
        cut.Find("#approver-search").Input("fa");
        Assert.Equal(["Add Farah Khan (not on the team yet)", "Add Faisal Iqbal"],
            cut.FindAll("[data-testid=person-result]").Select(b => b.TextContent.Trim()));
        cut.FindAll("[data-testid=person-result]")[0].Click();
        Assert.Equal(["Farah Khan"], cut.FindAll("[data-testid=chosen-approver] .approver-name").Select(n => n.TextContent.Trim()));
        Assert.Equal(["All (1)", "1"], cut.FindAll("#gate-required option").Select(o => o.TextContent.Trim()));
        cut.Find("#gate-required").Change("1");
        cut.Find("#gate-exclude").Change(false);
        Click(cut, "gate-setup-dialog", "Save gate");

        var setUp = Assert.Single(_stages.SetUps);
        Assert.Equal(("CBU-2", 1, false), (setUp.Key, setUp.Required, setUp.Exclude));
        Assert.Equal([FakeStageGateService.Farah], setUp.Approvers);
        Assert.Contains("Saved the Planning gate.", _announced);
    }

    [Fact]
    public void P3_US3_AS3_Requesting_approval_shows_the_open_items_first()
    {
        _stages.View = _stages.View with { Stages = [NotRequested(_stages.View.Stages[0]), .. _stages.View.Stages.Skip(1)] };
        _stages.OpenItems = [new ContainedItemView("CBU-5", WorkItemType.Task, "Write the charter", FakeBacklogService.ToDo, null, null, [1])];
        var cut = RenderStages();

        Stage(cut, "CBU-1").QuerySelector("[data-testid=request-approval]")!.Click();

        Assert.Contains("CBU-5 Write the charter", cut.Find("[data-testid=request-dialog]").TextContent, StringComparison.Ordinal);
        Assert.Empty(_stages.Requests);
        Click(cut, "request-dialog", "Request approval");
        Assert.Equal(["CBU-1"], _stages.Requests);
        Assert.Null(Stage(cut, "CBU-2").QuerySelector("[data-testid=request-approval]")); // locked
    }

    [Fact]
    public void A_pending_request_can_be_withdrawn()
    {
        var cut = RenderStages();

        Stage(cut, "CBU-1").QuerySelector("[data-testid=withdraw-request]")!.Click();

        Assert.Equal([("CBU-1", 1)], _stages.Withdrawals);
    }

    [Fact]
    public void P3_US3_AS4_An_approver_approves_with_an_optional_note()
    {
        var cut = RenderStages();

        Stage(cut, "CBU-1").QuerySelector("[data-testid=approve-gate]")!.Click();
        Click(cut, "decision-dialog", "Approve");

        Assert.Equal([("CBU-1", 1, GateDecisionKind.Approve, (string?)null)], _stages.Decisions);
    }

    [Fact]
    public void P3_US3_AS5_Sending_back_needs_a_note()
    {
        var cut = RenderStages();

        Stage(cut, "CBU-1").QuerySelector("[data-testid=send-back-gate]")!.Click();
        Click(cut, "decision-dialog", "Send back");
        Assert.Empty(_stages.Decisions);
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("[data-testid=decision-error]").TextContent));

        cut.Find("#gate-note").Input("The budget is missing.");
        Click(cut, "decision-dialog", "Send back");

        Assert.Equal([("CBU-1", 1, GateDecisionKind.SendBack, (string?)"The budget is missing.")], _stages.Decisions);
    }

    [Fact]
    public void P3_FR039_Locked_stages_move_up_and_down_among_themselves()
    {
        var cut = RenderStages();

        Assert.Null(Stage(cut, "CBU-2").QuerySelector("[data-testid=move-stage-up]"));
        Assert.Null(Stage(cut, "CBU-4").QuerySelector("[data-testid=move-stage-down]"));
        Assert.Null(Stage(cut, "CBU-1").QuerySelector("[data-testid=move-stage-down]"));
        Stage(cut, "CBU-3").QuerySelector("[data-testid=move-stage-up]")!.Click();

        Assert.Equal([("CBU-3", -1)], _stages.Moves);
        cut.WaitForAssertion(() => Assert.Equal(["Initiation", "Execution", "Planning", "Closure"],
            cut.FindAll("[data-testid=stage-title]").Select(t => t.TextContent.Trim())));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusById" && Equals(i.Arguments[0], "stage-CBU-3"));
        Assert.Contains("Moved Execution up: it is now stage 2 of 4.", _announced);
    }

    [Fact]
    public void P3_FR039_Project_Admins_add_a_stage_at_the_end()
    {
        var cut = RenderStages();

        cut.Find("#new-stage-title").Input("Transition");
        cut.Find("[data-testid=add-stage]").Click();

        Assert.Equal([("CBU", "Transition", (string?)null)], _stages.Added);
    }

    [Fact]
    public void A_conflict_is_explained_and_the_stages_are_read_again()
    {
        _stages.NextError = AppError.Conflict("This request has ended. The gate's current state is shown.");
        var cut = RenderStages();
        var loads = _stages.Loads;

        Stage(cut, "CBU-1").QuerySelector("[data-testid=withdraw-request]")!.Click();

        cut.WaitForAssertion(() => Assert.Contains("This request has ended.", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.True(_stages.Loads > loads);
    }

    [Fact]
    public void P3_FR055_Viewers_who_are_not_approvers_change_nothing()
    {
        _stages.View = _stages.View with
        {
            CanContribute = false,
            CanManage = false,
            Stages = [.. _stages.View.Stages.Select(s => s with { Gate = s.Gate with { CanDecide = false } })],
        };

        var cut = RenderStages();

        Assert.Empty(cut.FindAll("[data-testid=stage] button"));
        Assert.Empty(cut.FindAll("#new-stage-title"));
    }

    [Fact]
    public void P3_FR037_A_Viewer_who_is_an_approver_can_decide_and_nothing_else()
    {
        _stages.View = _stages.View with { CanContribute = false, CanManage = false };

        var cut = RenderStages();

        var initiation = Stage(cut, "CBU-1");
        Assert.NotNull(initiation.QuerySelector("[data-testid=approve-gate]"));
        Assert.Null(initiation.QuerySelector("[data-testid=withdraw-request]"));
        Assert.Equal(2, cut.FindAll("[data-testid=stage] button").Count);
    }

    [Fact]
    public void The_address_can_point_at_a_stage()
    {
        RenderStages("?stage=CBU-3");

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusById" && Equals(i.Arguments[0], "stage-CBU-3"));
    }

    [Fact]
    public void Projects_without_stages_show_not_found()
    {
        _stages.View = _stages.View with { ProjectKey = "WEB" };

        var cut = RenderStages();

        Assert.Contains("Not found", cut.Find("h1").TextContent, StringComparison.Ordinal);
    }
}
