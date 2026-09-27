using Microsoft.EntityFrameworkCore;
using Upms.Application.Identity;
using Upms.Infrastructure.Persistence;

namespace Upms.Infrastructure.Identity;

/// <summary>A SQL Server application lock owned by the caller's transaction, so commit or rollback releases it. A
/// change that cannot get it within 15 seconds fails rather than acting on data another change is still altering.</summary>
internal sealed class AdministratorLock(AppDbContext db) : IAdministratorLock
{
    private const string AcquireSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock @Resource = N'upms:administrators', @LockMode = N'Exclusive',
            @LockOwner = N'Transaction', @LockTimeout = 15000;
        IF @result < 0 THROW 51000, N'Another change to the administrators is still in progress.', 1;
        """;

    public async Task AcquireAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The administrators lock must be taken inside a transaction.");
        }

        await db.Database.ExecuteSqlRawAsync(AcquireSql, ct);
    }
}
