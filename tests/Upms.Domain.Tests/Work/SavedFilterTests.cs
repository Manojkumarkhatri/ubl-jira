using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Saved filters: named searches that belong to one person and can be shared with projects (Phase 4 FR-039,
/// FR-040, research R17).</summary>
public sealed class SavedFilterTests
{
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void P4_US3_AS8_A_filter_keeps_its_name_query_and_owner()
    {
        var filter = SavedFilter.Create(Amina, "  Unassigned refunds ", "q=refund&assignee=none", T0).Value!;

        Assert.Equal((Amina, "Unassigned refunds", "UNASSIGNED REFUNDS", "q=refund&assignee=none", T0, T0),
            (filter.OwnerId, filter.Name, filter.NormalizedName, filter.Query, filter.CreatedAt, filter.UpdatedAt));
        Assert.Empty(filter.Shares);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_filter_needs_a_name(string name)
    {
        Assert.Equal("Name", SavedFilter.Create(Amina, name, "q=x", T0).Error!.Field);
    }

    [Fact]
    public void Names_have_at_most_80_characters_and_queries_2000()
    {
        Assert.True(SavedFilter.Create(Amina, new string('n', 80), "q=x", T0).IsSuccess);
        Assert.Equal("Name", SavedFilter.Create(Amina, new string('n', 81), "q=x", T0).Error!.Field);
        Assert.Equal("Query", SavedFilter.Create(Amina, "Long", new string('q', 2_001), T0).Error!.Field);
    }

    [Fact]
    public void Names_are_compared_ignoring_case_and_surrounding_spaces()
    {
        Assert.Equal(SavedFilter.Normalize("unassigned Refunds "), SavedFilter.Normalize(" UNASSIGNED refunds"));
    }

    [Fact]
    public void Renaming_and_updating_change_the_time()
    {
        var filter = SavedFilter.Create(Amina, "Refunds", "q=refund", T0).Value!;

        Assert.Null(filter.Rename("Open refunds", T0.AddMinutes(1)));
        Assert.Null(filter.Update("q=refund&category=ToDo", T0.AddMinutes(2)));

        Assert.Equal(("Open refunds", "OPEN REFUNDS", "q=refund&category=ToDo", T0.AddMinutes(2)),
            (filter.Name, filter.NormalizedName, filter.Query, filter.UpdatedAt));
        Assert.Equal("Name", filter.Rename(" ", T0)!.Field);
    }

    [Fact]
    public void P4_US3_AS10_Sharing_replaces_the_projects_shared_with()
    {
        var filter = SavedFilter.Create(Amina, "Refunds", "q=refund", T0).Value!;

        filter.ShareWith([1, 2], T0);
        filter.ShareWith([2, 3, 3], T0.AddMinutes(1));

        Assert.Equal([2L, 3L], filter.Shares.Select(s => s.ProjectId).Order());
        Assert.Equal(T0, filter.Shares.Single(s => s.ProjectId == 2).SharedAt);
    }
}
