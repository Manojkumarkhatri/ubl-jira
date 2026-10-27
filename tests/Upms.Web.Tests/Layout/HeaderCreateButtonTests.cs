using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Web.Components.Layout;

namespace Upms.Web.Tests.Layout;

/// <summary>The header's Create button (Jira parity pass FR-001; US1 scenario 8).</summary>
public sealed class HeaderCreateButtonTests : BunitTestBase
{
    public HeaderCreateButtonTests()
    {
        Services.AddSingleton<Upms.Application.Work.ISearchService>(new Fakes.FakeSearchService());
        Services.AddSingleton<Upms.Application.Notifications.INotificationService>(new Fakes.FakeNotificationService());
    }

    [Fact]
    public void JP_US1_AS1_Create_is_in_the_header_and_opens_the_form()
    {
        string? requested = "none";
        Services.GetRequiredService<CreateWorkLauncher>().Requested += (key, _, _) => requested = key;
        var cut = Render<Header>();

        cut.Find("[data-testid=create-button]").Click();

        Assert.Null(requested); // the form takes the space from the page
    }

    [Fact]
    public void JP_US1_AS8_Someone_who_can_add_work_nowhere_gets_no_Create_button()
    {
        CreateWork.Spaces.Clear();

        var cut = Render<Header>();

        Assert.Empty(cut.FindAll("[data-testid=create-button]"));
    }

    [Fact]
    public void Someone_who_makes_their_first_space_gets_Create_on_the_next_page()
    {
        var spaces = CreateWork.Spaces.ToList();
        CreateWork.Spaces.Clear();
        var cut = Render<Header>();

        CreateWork.Spaces.AddRange(spaces);
        cut.InvokeAsync(() => Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("spaces/DATA/board"));

        cut.WaitForElement("[data-testid=create-button]");
    }
}
