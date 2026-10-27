using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>The order of a Stage-gate project's stages: which is current, which are locked, and where stages may go
/// (Phase 3 FR-031, FR-032, FR-039; research R13).</summary>
public sealed class StageOrderTests
{
    private static StageSlot Slot(long id, string rank, bool approved = false) => new(id, $"CBU-{id}", $"Stage {id}", rank, approved);

    // Initiation approved; Planning current; Execution and Closure locked. Given out of order on purpose.
    private static StageOrder Order() => new([Slot(4, "a3"), Slot(2, "a1"), Slot(1, "a0", approved: true), Slot(3, "a2")]);

    [Fact]
    public void P3_FR031_Stages_are_ordered_by_rank_then_id()
    {
        var order = new StageOrder([Slot(3, "a1"), Slot(2, "a1"), Slot(1, "a0")]);

        Assert.Equal([1L, 2L, 3L], order.Stages.Select(s => s.Id));
    }

    [Fact]
    public void P3_FR032_The_current_stage_is_the_first_not_approved_and_later_stages_are_locked()
    {
        var order = Order();

        Assert.Equal(2, order.Current?.Id);
        Assert.Equal([StagePlace.Approved, StagePlace.Current, StagePlace.Locked, StagePlace.Locked],
            order.Stages.Select(s => order.PlaceOf(s.Id)));
        Assert.True(order.IsLocked(3));
        Assert.False(order.IsLocked(2));
        Assert.Equal(2, order.BlockingGateFor(4)?.Id);
        Assert.Null(order.BlockingGateFor(2));
    }

    [Fact]
    public void The_first_stage_is_never_locked_and_with_every_gate_approved_none_is()
    {
        var fresh = new StageOrder([Slot(1, "a0"), Slot(2, "a1")]);
        Assert.Equal(StagePlace.Current, fresh.PlaceOf(1));

        var done = new StageOrder([Slot(1, "a0", true), Slot(2, "a1", true)]);
        Assert.Null(done.Current);
        Assert.All(done.Stages, s => Assert.False(done.IsLocked(s.Id)));
    }

    [Fact]
    public void P3_FR039_New_stages_go_after_the_current_stage_or_at_the_end()
    {
        var order = Order();

        Assert.Null(order.CheckAddAfter(null, out var before, out var after));
        Assert.Equal(("a3", (string?)null), (before, after));
        Assert.Null(order.CheckAddAfter(2, out before, out after));
        Assert.Equal(("a1", (string?)"a2"), (before, after));
        Assert.Equal(StageOrder.FixedCode, order.CheckAddAfter(1, out _, out _)?.Code);
    }

    [Fact]
    public void P3_FR039_Locked_stages_move_among_themselves()
    {
        var order = Order();

        Assert.Null(order.CheckMove(4, -1, out var before, out var after));
        Assert.Equal(("a1", (string?)"a2"), (before, after));
        Assert.Null(order.CheckMove(3, +1, out before, out after));
        Assert.Equal(("a3", (string?)null), (before, after));
    }

    [Theory]
    [InlineData(1, 1)]    // approved
    [InlineData(2, 1)]    // current
    [InlineData(3, -1)]   // would go before the current stage
    [InlineData(4, 1)]    // already last
    public void P3_FR039_Approved_and_current_stages_keep_their_places(long stageId, int offset)
    {
        Assert.Equal(StageOrder.FixedCode, Order().CheckMove(stageId, offset, out _, out _)?.Code);
    }
}
