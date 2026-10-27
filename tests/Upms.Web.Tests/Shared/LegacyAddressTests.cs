using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Upms.Web.Components.Shared;

namespace Upms.Web.Tests.Shared;

/// <summary>Addresses from Phases 1 to 4 lead to the same page at its new address (Jira parity pass FR-020; research R2).</summary>
public sealed class LegacyAddressTests : BunitTestBase
{
    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private NavigationHistory Open(string oldAddress)
    {
        Navigation.NavigateTo(oldAddress);
        Render<LegacyAddress>();
        return Navigation.History.First();
    }

    [Theory]
    [InlineData("projects", "http://localhost/spaces")]
    [InlineData("projects/WEB/board", "http://localhost/spaces/WEB/board")]
    [InlineData("projects/WEB/board?task=WEB-1&labels=payments", "http://localhost/spaces/WEB/board?task=WEB-1&labels=payments")]
    [InlineData("projects/MOB/sprints/3", "http://localhost/spaces/MOB/sprints/3")]
    [InlineData("projects/CBU/stages?stage=CBU-2", "http://localhost/spaces/CBU/stages?stage=CBU-2")]
    [InlineData("my-tasks?due=overdue", "http://localhost/for-you/assigned?due=overdue")]
    public void JP_US4_AS2_Old_addresses_lead_to_new_pages(string oldAddress, string newAddress)
    {
        var landed = Open(oldAddress);

        Assert.Equal(newAddress, Navigation.ToAbsoluteUri(landed.Uri).ToString());
        Assert.True(landed.Options.ReplaceHistoryEntry, "The old address must not stay in the browser history.");
    }

    [Fact]
    public void Only_the_first_segment_changes()
    {
        var landed = Open("projects/PROJECTS/list?q=projects");

        Assert.Equal("http://localhost/spaces/PROJECTS/list?q=projects", Navigation.ToAbsoluteUri(landed.Uri).ToString());
    }
}
