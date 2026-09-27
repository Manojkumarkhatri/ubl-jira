using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Upms.Application;
using Upms.Application.Common;
using Upms.Infrastructure;

namespace Upms.Application.Tests.Fixtures;

/// <summary>The Application and Infrastructure services wired as in production, with a fake clock,
/// a settable caller and a real SQL Server. Each <see cref="CallAsync{TService,TResult}"/> runs in its
/// own scope, like one user action in the app.</summary>
public sealed class ServiceHarness : IAsyncDisposable
{
    public const string SetupToken = "test-setup-token-5f0c";

    private readonly ServiceProvider _provider;

    public ServiceHarness(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Setup:Token"] = SetupToken,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddUpmsApplication(configuration);
        services.AddUpmsInfrastructure(configuration);
        services.AddAuthentication().AddIdentityCookies();

        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton<IHttpContextAccessor>(HttpContextAccessor);
        services.AddSingleton<IAuthenticationService, NoOpAuthenticationService>();

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        HttpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = _provider };
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero));

    public TestCurrentUser CurrentUser { get; } = new();

    public FixedHttpContextAccessor HttpContextAccessor { get; } = new();

    public async Task<TResult> CallAsync<TService, TResult>(Func<TService, Task<TResult>> call)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task CallAsync<TService>(Func<TService, Task> call)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    /// <summary>An HTTP context accessor with a plain field, so the context is visible on every thread.</summary>
    public sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    /// <summary>Sign-in writes a cookie in the app; tests only need the sign-in result.</summary>
    private sealed class NoOpAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
