using Upms.Domain.Common;

namespace Upms.Domain.Tests.Common;

/// <summary>Fractional indexing for card order (research R14).</summary>
public sealed class RankTests
{
    [Fact]
    public void Keys_before_between_and_after_sort_ordinally()
    {
        var middle = Rank.First();
        var before = Rank.Before(middle);
        var after = Rank.After(middle);
        var between = Rank.Between(middle, after);

        var sorted = new[] { after, between, middle, before }.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        Assert.Equal([before, middle, between, after], sorted);
    }

    [Fact]
    public void Appending_many_keys_keeps_them_short_and_ordered()
    {
        var keys = new List<string> { Rank.First() };
        for (var i = 0; i < 5000; i++)
        {
            keys.Add(Rank.After(keys[^1]));
        }

        Assert.Equal(keys, keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(keys, k => Assert.True(k.Length <= 4, $"'{k}' is too long"));
    }

    [Fact]
    public void Prepending_many_keys_keeps_them_short_and_ordered()
    {
        var keys = new List<string> { Rank.First() };
        for (var i = 0; i < 5000; i++)
        {
            keys.Insert(0, Rank.Before(keys[0]));
        }

        Assert.Equal(keys, keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(keys, k => Assert.True(k.Length <= 4, $"'{k}' is too long"));
    }

    [Fact]
    public void A_thousand_inserts_into_the_same_gap_stay_ordered()
    {
        var low = Rank.First();
        var high = Rank.After(low);
        var inserted = new List<string>();

        for (var i = 0; i < 1000; i++)
        {
            var key = Rank.Between(low, high);
            Assert.True(string.CompareOrdinal(low, key) < 0 && string.CompareOrdinal(key, high) < 0);
            inserted.Add(key);
            high = key; // always insert directly after "low"
        }

        Assert.Equal(inserted.Count, inserted.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Long_keys_report_that_the_column_needs_rebalancing()
    {
        Assert.False(Rank.NeedsRebalance(Rank.First()));
        Assert.False(Rank.NeedsRebalance(new string('V', Rank.RebalanceThreshold)));
        Assert.True(Rank.NeedsRebalance(new string('V', Rank.RebalanceThreshold + 1)));
    }

    [Fact]
    public void A_sequence_gives_evenly_increasing_short_keys()
    {
        var keys = Rank.Sequence(2000);

        Assert.Equal(2000, keys.Count);
        Assert.Equal(keys, keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.True(k.Length <= 4));
    }

    [Fact]
    public void Between_refuses_keys_in_the_wrong_order()
    {
        var low = Rank.First();
        var high = Rank.After(low);

        Assert.Throws<ArgumentException>(() => Rank.Between(high, low));
        Assert.Throws<ArgumentException>(() => Rank.Between(low, low));
    }
}
