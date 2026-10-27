using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Drawer;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Drawer;

/// <summary>The drawer's labels (Phase 4 FR-042–FR-045; contracts/ui-routes.md): chips named "Remove label X", an "Add
/// label" combobox with suggestions, and read-only labels for Viewers.</summary>
public sealed class LabelsSectionTests : BunitTestBase
{
    private readonly FakeLabelService _labels = new();
    private int _changes;

    public LabelsSectionTests()
    {
        _labels.OnItems["WEB-1"] = ["design"];
        _labels.InUse.AddRange(["landing-page", "launch", "Design"]);
        Services.AddSingleton<ILabelService>(_labels);
    }

    private IRenderedComponent<LabelsSection> Show(bool canEdit = true) => Render<LabelsSection>(p => p
        .Add(x => x.WorkItemKey, "WEB-1").Add(x => x.ProjectKey, "MOB").Add(x => x.Initial, ["design"]).Add(x => x.CanEdit, canEdit)
        .Add(x => x.OnChanged, () => _changes++));

    private static List<string> Chips(IRenderedComponent<LabelsSection> cut) =>
        cut.FindAll(".label-list .label-chip").Select(c => c.FirstChild!.TextContent.Trim()).ToList();

    [Fact]
    public void P4_US4_AS2_Typing_suggests_labels_in_use_that_the_item_does_not_have()
    {
        var cut = Show();

        cut.Find("#label-input").Input("la");

        Assert.Equal(("MOB", "la"), _labels.Suggested.Single());
        Assert.Equal("true", cut.Find("#label-input").GetAttribute("aria-expanded"));
        Assert.Equal(["landing-page", "launch"], cut.FindAll("#label-suggestions [role=option]").Select(o => o.TextContent));
        cut.Find("#label-input").Input("des");
        Assert.Empty(cut.FindAll("#label-suggestions")); // "Design" is already on the item
    }

    [Fact]
    public void P4_US4_AS1_Enter_adds_the_chosen_suggestion()
    {
        var cut = Show();
        cut.Find("#label-input").Input("la");

        cut.Find("#label-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("#label-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("label-option-1", cut.Find("#label-input").GetAttribute("aria-activedescendant"));
        cut.Find("#label-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal(["design", "launch"], Chips(cut)));
        Assert.Equal("", cut.Find("#label-input").GetAttribute("value"));
        Assert.Equal("Added the label launch.", cut.Find("[data-testid=labels] [role=status]").TextContent);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void P4_US4_AS1_A_space_ends_a_new_label()
    {
        var cut = Show();

        cut.Find("#label-input").Input("release-blocker ");

        cut.WaitForAssertion(() => Assert.Equal(["design", "release-blocker"], Chips(cut)));
        Assert.Equal(["design", "release-blocker"], _labels.OnItems["WEB-1"]);
    }

    [Fact]
    public void A_refused_label_is_explained_and_kept_for_correction()
    {
        var cut = Show();

        cut.Find("#label-input").Input("a,b");
        cut.Find("#label-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal("A label can't contain spaces or commas and can have up to 50 characters.",
            cut.Find("#label-error").TextContent));
        Assert.Equal("a,b", cut.Find("#label-input").GetAttribute("value"));
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void P4_US4_AS5_Each_chip_removes_its_label()
    {
        var cut = Show();

        cut.Find("[aria-label='Remove label design']").Click();

        cut.WaitForAssertion(() => Assert.Empty(Chips(cut)));
        Assert.Equal("Removed the label design.", cut.Find("[data-testid=labels] [role=status]").TextContent);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void Escape_closes_the_suggestions()
    {
        var cut = Show();
        cut.Find("#label-input").Input("la");

        cut.Find("#label-input").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", cut.Find("#label-input").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void P4_US4_AS8_Viewers_see_the_labels_only()
    {
        var cut = Show(canEdit: false);

        Assert.Equal(["design"], Chips(cut));
        Assert.Empty(cut.FindAll("#label-input"));
        Assert.Empty(cut.FindAll(".chip-remove"));
    }
}
