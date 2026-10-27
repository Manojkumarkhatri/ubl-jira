using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>How text becomes search words, and what looks like a key (Phase 4 FR-032, FR-034, research R15, R16).</summary>
public sealed class SearchWordsTests
{
    [Fact]
    public void Words_are_lower_cased_split_on_anything_but_letters_and_digits_and_distinct()
    {
        Assert.Equal(["refund", "card", "v2", "api", "über", "café"],
            SearchWords.Of("Refund (card) — refund, V2/API: Über-café!"));
    }

    [Fact]
    public void Words_are_kept_from_2_to_40_characters()
    {
        var long50 = new string('x', 50);

        Assert.Equal(["ab", new string('x', 40)], SearchWords.Of($"a ab {long50}"));
    }

    [Fact]
    public void Empty_text_has_no_words()
    {
        Assert.Empty(SearchWords.Of(null));
        Assert.Empty(SearchWords.Of("  -- ! "));
    }

    [Fact]
    public void A_search_uses_at_most_10_words()
    {
        Assert.Equal(10, SearchWords.Query("one two three four five six seven eight nine ten eleven twelve").Count);
    }

    [Theory]
    [InlineData("MOB-12", "MOB-12")]
    [InlineData("mob-12", "MOB-12")]
    [InlineData("  Web2-7 ", "WEB2-7")]
    [InlineData("ABCDEFGHIJ-1", "ABCDEFGHIJ-1")]
    public void P4_US3_AS1_Key_like_text_is_recognized_regardless_of_case(string text, string key)
    {
        Assert.True(SearchWords.LooksLikeKey(text, out var found));
        Assert.Equal(key, found);
    }

    [Theory]
    [InlineData("refund")]
    [InlineData("M-12")]
    [InlineData("ABCDEFGHIJK-1")]
    [InlineData("MOB-")]
    [InlineData("MOB-12 refund")]
    [InlineData("MOB_12")]
    public void Other_text_is_not_a_key(string text)
    {
        Assert.False(SearchWords.LooksLikeKey(text, out _));
    }
}
