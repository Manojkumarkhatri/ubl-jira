using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>"B waits for A" between items of one project: which links are refused, and when a link is late (Phase 3
/// FR-047, FR-048, FR-051).</summary>
public sealed class DependencyGraphTests
{
    private static readonly DateOnly Nov10 = new(2026, 11, 10);
    private static readonly DateOnly Nov12 = new(2026, 11, 12);

    private static DependencyNode Node(long id, long? parent = null, long? container = null) => new(id, parent, container);

    [Fact]
    public void An_item_cannot_wait_for_itself()
    {
        Assert.Equal(DependencyGraph.ToSelfCode, DependencyGraph.CheckNew(Node(1), Node(1), [])?.Code);
    }

    [Fact]
    public void A_dependency_is_recorded_once()
    {
        Assert.Equal(DependencyGraph.DuplicateCode, DependencyGraph.CheckNew(Node(2), Node(1), [(1, 2)])?.Code);
    }

    [Theory]
    [InlineData(2, null, null, 1, 2, null)]   // a sub-task waits for its parent
    [InlineData(1, null, null, 2, 1, null)]   // a parent waits for its sub-task
    [InlineData(2, null, 1, 1, null, null)]   // an item waits for its epic or stage
    [InlineData(1, null, null, 2, null, 1)]   // an epic or stage waits for its item
    public void P3_FR048_Links_within_one_hierarchy_are_refused(int waitingId, int? waitingParent, int? waitingContainer,
        int waitedForId, int? waitedForParent, int? waitedForContainer)
    {
        var error = DependencyGraph.CheckNew(Node(waitingId, waitingParent, waitingContainer), Node(waitedForId, waitedForParent, waitedForContainer), []);

        Assert.Equal(DependencyGraph.InHierarchyCode, error?.Code);
    }

    [Fact]
    public void P3_US4_AS7_A_direct_loop_is_refused()
    {
        // CBU-12 (2) waits for CBU-11 (1); CBU-11 cannot then wait for CBU-12.
        Assert.Equal(DependencyGraph.LoopCode, DependencyGraph.CheckNew(Node(1), Node(2), [(1, 2)])?.Code);
    }

    [Fact]
    public void P3_US4_AS7_A_loop_through_other_items_is_refused()
    {
        // 2 waits for 1, 3 waits for 2, 4 waits for 3: 1 cannot wait for 4.
        (long, long)[] links = [(1, 2), (2, 3), (3, 4)];

        Assert.Equal(DependencyGraph.LoopCode, DependencyGraph.CheckNew(Node(1), Node(4), links)?.Code);
        Assert.Null(DependencyGraph.CheckNew(Node(4), Node(1), links));
        Assert.Null(DependencyGraph.CheckNew(Node(5), Node(3), links));
    }

    [Fact]
    public void P3_FR051_A_link_is_late_while_the_waiting_item_starts_before_the_other_is_due()
    {
        Assert.True(DependencyGraph.IsLate(waitedForOpen: true, waitedForDue: Nov12, waitingStart: Nov10, waitingDue: null));
        Assert.False(DependencyGraph.IsLate(waitedForOpen: true, waitedForDue: Nov10, waitingStart: Nov10, waitingDue: null));
        Assert.False(DependencyGraph.IsLate(waitedForOpen: false, waitedForDue: Nov12, waitingStart: Nov10, waitingDue: null));
    }

    [Fact]
    public void P3_FR051_Without_a_start_the_waiting_item_s_due_date_counts_as_it_does_for_a_milestone()
    {
        Assert.True(DependencyGraph.IsLate(true, Nov12, waitingStart: null, waitingDue: Nov10));
        Assert.False(DependencyGraph.IsLate(true, Nov12, waitingStart: null, waitingDue: Nov12));
    }

    [Fact]
    public void P3_FR051_A_link_missing_a_date_is_never_late()
    {
        Assert.False(DependencyGraph.IsLate(true, waitedForDue: null, waitingStart: Nov10, waitingDue: Nov12));
        Assert.False(DependencyGraph.IsLate(true, waitedForDue: Nov12, waitingStart: null, waitingDue: null));
    }

    [Fact]
    public void A_new_dependency_names_both_ends()
    {
        var context = ChangeContext.New(Guid.NewGuid(), new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));

        var dependency = Dependency.Create(7, predecessorId: 1, successorId: 2, context);

        Assert.Equal((7L, 1L, 2L, context.ActorId, context.At),
            (dependency.ProjectId, dependency.PredecessorId, dependency.SuccessorId, dependency.CreatedById, dependency.CreatedAt));
    }
}
