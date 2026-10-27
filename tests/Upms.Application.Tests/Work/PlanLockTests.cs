using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The per-project planning lock (Phase 3 research R9): changes whose rules span several rows of one project
/// run one at a time; other projects are not held up.</summary>
public sealed class PlanLockTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly TimeSpan Blocked = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task A_second_change_to_the_same_project_waits_for_the_first()
    {
        await using var first = Harness.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var firstTransaction = await firstDb.Database.BeginTransactionAsync(Ct);
        await first.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct);

        await using var second = Harness.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var secondTransaction = await secondDb.Database.BeginTransactionAsync(Ct);
        var waiting = second.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct);

        Assert.NotSame(waiting, await Task.WhenAny(waiting, Task.Delay(Blocked, Ct)));

        await firstTransaction.CommitAsync(Ct);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Fact]
    public async Task Another_project_is_not_held_up()
    {
        await using var first = Harness.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var firstTransaction = await firstDb.Database.BeginTransactionAsync(Ct);
        await first.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct);

        await using var second = Harness.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var secondTransaction = await secondDb.Database.BeginTransactionAsync(Ct);

        await second.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(2, Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task A_rollback_releases_the_lock()
    {
        await using var first = Harness.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using (var firstTransaction = await firstDb.Database.BeginTransactionAsync(Ct))
        {
            await first.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct);
            await firstTransaction.RollbackAsync(Ct);
        }

        await using var second = Harness.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var secondTransaction = await secondDb.Database.BeginTransactionAsync(Ct);

        await second.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task The_lock_is_taken_only_inside_a_transaction()
    {
        await using var scope = Harness.CreateScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IPlanLock>().AcquireAsync(1, Ct));
    }
}
