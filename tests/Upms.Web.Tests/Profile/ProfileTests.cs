using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Identity;
using Upms.Domain.Common;
using Upms.Web.Components.Pages.Profile;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Profile;

/// <summary>The profile's email choice (Phase 4 FR-051; contracts/ui-routes.md): a labelled radio group with the current
/// choice selected, saved for the signed-in person only.</summary>
public sealed class ProfileTests : BunitTestBase
{
    private readonly FakeAccountService _accounts = new() { EmailChoice = EmailChoice.AimedAtMe };

    public ProfileTests()
    {
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddScoped<LiveAnnouncer>();
    }

    [Fact]
    public void P4_FR051_The_three_choices_are_a_radio_group_with_the_current_one_selected()
    {
        var cut = Render<Upms.Web.Components.Pages.Profile.Profile>();

        var radios = cut.FindAll("input[name=email-choice]");
        Assert.Equal(["All", "AimedAtMe", "None"], radios.Select(r => r.GetAttribute("value")));
        Assert.Equal(["All notifications", "Only notifications aimed at me", "None"],
            radios.Select(r => cut.Find($"label[for={r.Id}]").TextContent));
        Assert.True(cut.Find("#email-AimedAtMe").HasAttribute("checked"));
        Assert.Equal("email-hint", cut.Find("input[name=email-choice]").Closest("fieldset")!.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void P4_FR051_Saving_stores_the_chosen_option()
    {
        var cut = Render<Upms.Web.Components.Pages.Profile.Profile>();

        cut.Find("#email-None").Change(true);
        cut.Find("[data-testid=save-email-choice]").Closest("form")!.Submit();

        cut.WaitForAssertion(() => Assert.Equal([EmailChoice.None], _accounts.ChoicesSet));
        Assert.True(cut.Find("#email-None").HasAttribute("checked"));
        Assert.Equal("Saved", cut.Find("[data-testid=save-email-choice] ~ .saved").TextContent);
    }
}
