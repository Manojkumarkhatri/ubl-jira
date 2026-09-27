using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Fixtures;

/// <summary>Base class for service tests against the shared SQL Server: resets the data and builds a
/// fresh <see cref="ServiceHarness"/> for every test.</summary>
public abstract class IntegrationTest(SqlServerFixture fixture) : IAsyncLifetime
{
    protected ServiceHarness Harness { get; private set; } = null!;

    protected TestData Data { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        Harness = new ServiceHarness(fixture.ConnectionString);
        Data = new TestData(Harness);
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Harness.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Makes <paramref name="user"/> the caller of the following service calls.</summary>
    protected void ActAs(User? user) => Harness.CurrentUser.UserId = user?.Id;

    protected Task<TResult> CallAsync<TService, TResult>(Func<TService, Task<TResult>> call)
        where TService : notnull => Harness.CallAsync(call);

    /// <summary>Reads the database directly, outside the services under test.</summary>
    protected async Task<TResult> QueryAsync<TResult>(Func<IAppDbContext, Task<TResult>> query)
    {
        await using var scope = Harness.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IAppDbContext>());
    }

    protected Task<List<AuditEvent>> AuditEventsAsync() =>
        QueryAsync(db => db.AuditEvents.AsNoTracking().OrderBy(e => e.Id).ToListAsync(Ct));

    protected Task<User> ReloadUserAsync(Guid id) =>
        QueryAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct));
}
