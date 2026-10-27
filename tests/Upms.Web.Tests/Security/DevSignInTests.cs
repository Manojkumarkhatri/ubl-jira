using Microsoft.AspNetCore.Hosting;
using Upms.Web.Tests.Fixtures;

namespace Upms.Web.Tests.Security;

/// <summary>The development sign-in (until the company's LDAP sign-in is connected for UAT) can never be switched on
/// outside Development: the app refuses to start.</summary>
public sealed class DevSignInTests(WebDatabaseFixture database)
{
    [Fact]
    public async Task Outside_Development_the_app_refuses_to_start_with_the_development_sign_in()
    {
        await using var factory = new UpmsWebApplicationFactory(database.NewDatabase())
            .WithWebHostBuilder(b => b.UseSetting("DevSignIn:UserName", "admin"));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("works only in Development", error.Message, StringComparison.Ordinal);
    }
}
