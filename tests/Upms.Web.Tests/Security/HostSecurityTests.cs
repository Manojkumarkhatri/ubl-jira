using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Upms.Domain.Identity;
using Upms.Web.Security;
using Upms.Web.Tests.Fixtures;

namespace Upms.Web.Tests.Security;

/// <summary>Host-level security (FR-001, FR-003, contracts/http-endpoints.md, research R9).</summary>
public sealed class HostSecurityTests(WebDatabaseFixture database) : IAsyncLifetime
{
    private UpmsWebApplicationFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _factory = new UpmsWebApplicationFactory(database.NewDatabase());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData("/")]
    [InlineData("/projects")]
    [InlineData("/projects/WEB/board")]
    [InlineData("/admin/users")]
    public async Task US1_AS1_Anonymous_requests_are_sent_to_sign_in(string path)
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WEB", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/setup")]
    [InlineData("/health/live")]
    public async Task Sign_in_setup_and_health_are_public(string path)
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task US1_AS2_A_temporary_password_must_be_replaced_before_anything_else()
    {
        await _factory.CreateUserAsync("amina", "temporary pass 16", mustChangePassword: true);
        using var client = _factory.CreateHttpsClient();

        using var login = await UpmsWebApplicationFactory.PostLoginAsync(client, "amina", "temporary pass 16");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var response = await client.GetAsync(new Uri("/projects", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/ChangePassword", response.Headers.Location!.OriginalString.Replace("https://localhost", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_signed_in_user_reaches_their_pages()
    {
        await _factory.CreateUserAsync("bilal", "my own long passphrase");
        using var client = _factory.CreateHttpsClient();

        using var login = await UpmsWebApplicationFactory.PostLoginAsync(client, "bilal", "my own long passphrase");
        using var response = await client.GetAsync(new Uri("/account/profile", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Security_headers_are_sent_on_every_response()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri("/Account/Login", UriKind.Relative), Ct);

        Assert.Contains("default-src 'self'", string.Join(";", response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Cross_origin_requests_to_the_Blazor_hub_are_rejected()
    {
        using var client = _factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_attempts_are_limited_to_10_per_minute_per_client()
    {
        using var client = _factory.CreateHttpsClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 1; attempt <= 11; attempt++)
        {
            using var response = await UpmsWebApplicationFactory.PostLoginAsync(client, "nobody", "wrong password here");
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(10));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }

    [Fact]
    public async Task Policy_checks_that_overlap_in_one_circuit_each_read_the_users_status_safely()
    {
        var admin = await _factory.CreateUserAsync("ada", "correct horse battery 9", role: OrganizationRole.Administrator);
        // One scope, like a Blazor circuit in which several components check a policy at the same time.
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString())], "test"));

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => authorization.AuthorizeAsync(user, AuthorizationPolicies.Administrator)));

        Assert.All(results, r => Assert.True(r.Succeeded));
    }
}
