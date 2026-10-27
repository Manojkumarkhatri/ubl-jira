using Microsoft.EntityFrameworkCore;
using Upms.Application.Work;

namespace Upms.Infrastructure.Persistence;

/// <summary>A SQL Server application lock per project, owned by the caller's transaction so commit or rollback releases
/// it, like <see cref="Identity.AdministratorLock"/>. A change that cannot get it within 15 seconds fails rather than
/// acting on data another change is still altering (Phase 3 research R9).</summary>
internal sealed class PlanLock(AppDbContext db) : IPlanLock
{
    private const string AcquireSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock @Resource = {0}, @LockMode = N'Exclusive',
            @LockOwner = N'Transaction', @LockTimeout = 15000;
        IF @result < 0 THROW 51000, N'Another change to this project''s plan is still in progress.', 1;
        """;

    public async Task AcquireAsync(long projectId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The planning lock must be taken inside a transaction.");
        }

        await db.Database.ExecuteSqlRawAsync(AcquireSql, [ResourceName(projectId)], ct);
    }

    private static string ResourceName(long projectId) =>
        "upms:plan:" + projectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
