using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Tests.Fixtures;

namespace Upms.Application.Tests.Identity;

/// <summary>Whether a person keeps the sidebar collapsed (Jira parity pass FR-021; research R5).</summary>
public sealed class SidebarChoiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task JP_US4_AS4_The_choice_is_saved_on_the_person_and_comes_back_with_the_profile()
    {
        ActAs(await Data.UserAsync("amina"));
        Assert.False((await CallAsync<IAccountService, Result<MyProfile>>(s => s.GetProfileAsync(Ct))).ValueOrThrow().SidebarCollapsed);

        var saved = (await CallAsync<IAccountService, Result<MyProfile>>(s => s.SetSidebarCollapsedAsync(true, Ct))).ValueOrThrow();

        Assert.True(saved.SidebarCollapsed);
        Assert.True((await CallAsync<IAccountService, Result<MyProfile>>(s => s.GetProfileAsync(Ct))).ValueOrThrow().SidebarCollapsed);
    }

    [Fact]
    public async Task Nobody_signed_in_is_refused()
    {
        ActAs(null);

        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IAccountService, Result<MyProfile>>(s => s.SetSidebarCollapsedAsync(true, Ct))).Error!.Kind);
    }
}
