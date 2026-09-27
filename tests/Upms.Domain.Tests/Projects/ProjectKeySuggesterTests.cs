using System.Text.RegularExpressions;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>Key suggestions from the project name (FR-011, US1_AS3).</summary>
public sealed partial class ProjectKeySuggesterTests
{
    [Theory]
    [InlineData("Website Revamp", "WR")]
    [InlineData("Customer Portal Release Two", "CPRT")]
    [InlineData("Marketing", "MAR")]
    [InlineData("QA", "QA")]
    [InlineData("  payroll   migration ", "PM")]
    [InlineData("Café Opening", "CO")]
    [InlineData("HR-2026 onboarding", "HO")]
    public void US1_AS3_A_key_is_suggested_from_the_name(string name, string expected)
    {
        Assert.Equal(expected, ProjectKeySuggester.Suggest(name));
    }

    [Theory]
    [InlineData("2026 Plan", "PLA")]
    [InlineData("3D Printing Lab", "DPL")]
    public void Leading_digits_are_dropped(string name, string expected)
    {
        Assert.Equal(expected, ProjectKeySuggester.Suggest(name));
    }

    [Fact]
    public void A_taken_key_gets_a_numeric_suffix()
    {
        var taken = new HashSet<string> { "WR", "WR2" };

        Assert.Equal("WR3", ProjectKeySuggester.Suggest("Website Revamp", taken.Contains));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("١٢٣")]
    public void Names_without_usable_letters_get_no_suggestion(string name)
    {
        Assert.Equal("", ProjectKeySuggester.Suggest(name));
    }

    [Theory]
    [InlineData("Website Revamp")]
    [InlineData("A very long project name with many many words in it")]
    [InlineData("Marketing")]
    [InlineData("x")]
    [InlineData("9")]
    [InlineData("Ünïcödé Wörds")]
    public void Suggestions_always_match_the_key_pattern_or_are_empty(string name)
    {
        var key = ProjectKeySuggester.Suggest(name, k => k.Length < 4);

        Assert.True(key.Length == 0 || KeyPattern().IsMatch(key), $"'{key}'");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex KeyPattern();
}
