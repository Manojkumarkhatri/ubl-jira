using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Upms.Domain.Identity;

namespace Upms.Web.Tests.Fixtures;

/// <summary>The real host over HTTPS test transport, with its own database.</summary>
public sealed partial class UpmsWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string SetupToken = "web-test-setup-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Setup:Token", SetupToken);
        builder.UseSetting("App:PublicBaseUrl", "https://localhost");
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public async Task<User> CreateUserAsync(string userName, string password, bool mustChangePassword = false,
        OrganizationRole role = OrganizationRole.User)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = userName,
            MustChangePassword = mustChangePassword,
            OrganizationRole = role,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return user;
    }

    /// <summary>Signs in through the real form (anti-forgery token included) and returns the response.</summary>
    public static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string userName, string password)
    {
        var page = await client.GetStringAsync(new Uri("/Account/Login", UriKind.Relative));
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = HiddenValue(page, "__RequestVerificationToken"),
            ["_handler"] = HiddenValue(page, "_handler"),
            ["Input.UserName"] = userName,
            ["Input.Password"] = password,
        };
        using var content = new FormUrlEncodedContent(form);
        return await client.PostAsync(new Uri("/Account/Login", UriKind.Relative), content);
    }

    public static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $@"<input[^>]*name=""{Regex.Escape(name)}""[^>]*value=""([^""]*)""");
        if (!match.Success)
        {
            match = Regex.Match(html, $@"<input[^>]*value=""([^""]*)""[^>]*name=""{Regex.Escape(name)}""");
        }

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException($"Hidden input '{name}' not found.");
    }
}
