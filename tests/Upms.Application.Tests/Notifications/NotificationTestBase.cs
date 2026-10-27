using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Notifications;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Work;
using Upms.Domain.Notifications;

namespace Upms.Application.Tests.Notifications;

/// <summary>WEB with its owner Amina (Board helpers), plus raising events straight through the notification center and
/// reading one's own notifications (Phase 4 research R8, R9).</summary>
public abstract class NotificationTestBase(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected Task<long> ProjectIdAsync(string key = "WEB") =>
        QueryAsync(db => db.Projects.Where(p => p.Key == key).Select(p => p.Id).SingleAsync(Ct));

    protected Task<long> ItemIdAsync(string key) =>
        QueryAsync(db => db.WorkItems.IgnoreQueryFilters().Where(w => w.Key == key).Select(w => w.Id).SingleAsync(Ct));

    /// <summary>Raises the events of one action as the Work module does, saving them unless <paramref name="save"/> is
    /// false.</summary>
    protected async Task RaiseAsync(bool save, params NotificationEvent[] action)
    {
        await using var scope = Harness.CreateScope();
        await scope.ServiceProvider.GetRequiredService<INotificationCenter>().RaiseAsync(action, Ct);
        if (save)
        {
            await scope.ServiceProvider.GetRequiredService<IAppDbContext>().SaveChangesAsync(Ct);
        }
    }

    protected Task RaiseAsync(params NotificationEvent[] action) => RaiseAsync(true, action);

    /// <summary>Every notification row, oldest first.</summary>
    protected Task<List<Notification>> NotificationRowsAsync() =>
        QueryAsync(db => db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToListAsync(Ct));

    protected Task<int> UnreadAsync() => CallAsync<INotificationService, int>(s => s.CountUnreadAsync(Ct));

    protected async Task<Page<NotificationView>> ListAsync(int page = 1) =>
        (await CallAsync<INotificationService, Result<Page<NotificationView>>>(s => s.ListAsync(new PageRequest(page, 20), Ct))).ValueOrThrow();
}
