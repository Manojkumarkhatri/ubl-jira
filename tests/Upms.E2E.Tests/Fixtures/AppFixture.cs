using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.E2E.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(AppFixture))]
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Upms.E2E.Tests.Fixtures;

/// <summary>The real app on Kestrel with its own SQL Server, set up with a first administrator
/// (tasks.md T050). Browser tests talk to <see cref="BaseUrl"/>.</summary>
public sealed class AppFixture : IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "first run passphrase 2026";
    private const string SetupToken = "e2e-setup-token";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private AppFactory? _factory;

    public string BaseUrl { get; private set; } = "";

    public IServiceProvider Services => _factory!.Services;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "UpmsE2E" }.ConnectionString;
        _factory = new AppFactory(connectionString, SetupToken);
        _factory.UseKestrel(0);
        _factory.StartServer();
        var addresses = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server did not report its address.");
        var port = new Uri(addresses.Addresses.First().Replace("[::]", "localhost", StringComparison.Ordinal)).Port;
        BaseUrl = $"http://localhost:{port}";

        using var scope = _factory.Services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        var result = await setup.CreateFirstAdministratorAsync(SetupToken, AdminUserName, "Ada Admin", "admin@example.com",
            AdminPassword, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }

    /// <summary>Creates a user directly (as an administrator would) and returns the password to use.</summary>
    public async Task<string> CreateUserAsync(string userName, string displayName, bool mustChangePassword = false)
    {
        var password = $"{new string(userName.Reverse().ToArray())} temporary pass 42";
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var result = await userManager.CreateAsync(new User
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = displayName,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTimeOffset.UtcNow,
        }, password);
        return result.Succeeded
            ? password
            : throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>Adds an existing user to a project's team directly (Phase 2: projects are members-only).</summary>
    public async Task AddMemberAsync(string projectKey, string userName, ProjectRole role = ProjectRole.Member)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var userId = await db.Users.Where(u => u.UserName == userName).Select(u => u.Id).SingleAsync();
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Key == projectKey);
        var added = project.AddMember(userId, role, project.OwnerId, DateTimeOffset.UtcNow);
        if (!added.IsSuccess)
        {
            throw new InvalidOperationException(added.Error!.Message);
        }

        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _sql.DisposeAsync();
    }

    private sealed class AppFactory(string connectionString, string setupToken) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets(); // outside Development, serve _framework and wwwroot from the build output
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Setup:Token", setupToken);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            // Every test signs in from 127.0.0.1; the real limit (10 per minute) is covered by HostSecurityTests.
            builder.UseSetting("RateLimiting:SignInPerMinute", "1000");
        }
    }
}
