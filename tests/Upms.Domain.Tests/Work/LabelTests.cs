using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Labels: shared across the organization, matched regardless of letter case, keeping the first spelling
/// (Phase 4 FR-042, FR-043, research R18).</summary>
public sealed class LabelTests
{
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void P4_US4_AS1_A_label_keeps_its_spelling_and_a_normalized_name()
    {
        var label = Label.Create("  release-blocker ").Value!;

        Assert.Equal(("release-blocker", "RELEASE-BLOCKER"), (label.Name, label.NormalizedName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tech debt")]
    [InlineData("tech\tdebt")]
    [InlineData("tech\u0007debt")]
    [InlineData("tech,debt")]
    [InlineData("tech debt")]
    public void P4_US4_AS1_A_label_has_no_whitespace_control_characters_or_commas(string name)
    {
        var error = Label.Create(name).Error!;

        Assert.Equal((Label.InvalidLabelCode, "Label", "A label can't contain spaces or commas and can have up to 50 characters."),
            (error.Code, error.Field, error.Message));
    }

    [Fact]
    public void A_label_has_at_most_50_characters()
    {
        Assert.True(Label.Create(new string('a', 50)).IsSuccess);
        Assert.Equal(Label.InvalidLabelCode, Label.Create(new string('a', 51)).Error!.Code);
    }

    [Theory]
    [InlineData("launch", "Launch")]
    [InlineData("LAUNCH", "launch")]
    [InlineData("données", "DONNÉES")]
    public void P4_US4_AS3_Names_match_regardless_of_letter_case(string first, string second)
    {
        Assert.Equal(Label.Normalize(first), Label.Normalize(second));
    }

    [Fact]
    public void Normalizing_uses_the_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("INFRA", Label.Normalize("infra"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void An_item_has_at_most_20_labels()
    {
        Assert.Null(Label.CheckRoom(19));
        var error = Label.CheckRoom(20)!;

        Assert.Equal((Label.TooManyLabelsCode, "An item can have up to 20 labels."), (error.Code, error.Message));
    }

    [Fact]
    public void A_label_on_an_item_says_who_added_it_and_when()
    {
        var link = WorkItemLabel.Add(12, 3, Amina, T0);

        Assert.Equal((12L, 3, Amina, T0), (link.WorkItemId, link.LabelId, link.AddedById, link.AddedAt));
    }
}
