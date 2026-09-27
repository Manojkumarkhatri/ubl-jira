using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application;
using Upms.Application.Common;
using Upms.Infrastructure;

namespace Upms.Performance.Tests;

/// <summary>The Application and Infrastructure services wired as in production. Each call runs in its own scope
/// as the given user, like one action in the app (research R2, per-action scopes).</summary>
public sealed class PerfHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public PerfHarness(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddUpmsApplication(configuration);
        services.AddUpmsInfrastructure(configuration);
        services.AddAuthentication().AddIdentityCookies();
        services.AddHttpContextAccessor();
        services.AddScoped<ScopedCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ScopedCurrentUser>());
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task<TResult> CallAsync<TService, TResult>(Guid userId, Func<TService, Task<TResult>> call)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedCurrentUser>().UserId = userId;
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private sealed class ScopedCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }
}
