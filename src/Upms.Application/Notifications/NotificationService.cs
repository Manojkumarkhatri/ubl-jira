using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Notifications;

namespace Upms.Application.Notifications;

internal sealed class NotificationService(
    IAppDbContext db,
    ICallerContext caller,
    IProjectAccess access,
    IWorkItemTitles titles,
    IUserDirectory users,
    TimeProvider time) : INotificationService, INotificationCleanup
{
    public const int PageSize = 20;

    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(90);

    /// <summary>How long sent, given-up or cancelled emails are kept (data-model.md, EmailMessage).</summary>
    public static readonly TimeSpan EmailsKeptFor = TimeSpan.FromDays(30);

    public async Task<int> CountUnreadAsync(CancellationToken ct) =>
        await MineAsync(tracked: false, ct) is { } mine ? await mine.CountAsync(n => n.ReadAt == null, ct) : 0;

    public async Task<Result<Page<NotificationView>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var request = new PageRequest(page.Page, PageSize).Normalized();
        if (await caller.GetAsync(ct) is not { IsActive: true })
        {
            return AppError.Forbidden();
        }

        if (await MineAsync(tracked: false, ct) is not { } mine)
        {
            return Page<NotificationView>.Empty(request);
        }

        var total = await mine.CountAsync(ct);
        var rows = await mine
            .OrderByDescending(n => n.OccurredAt).ThenByDescending(n => n.Id)
            .Skip(request.Skip).Take(request.PageSize)
            .ToListAsync(ct);
        var items = await titles.GetAsync(rows.ConvertAll(n => n.WorkItemId), ct);
        var actors = await users.GetAsync(rows.Where(n => n.ActorId is not null).Select(n => n.ActorId!.Value).ToList(), ct);
        var views = rows
            .Where(n => items.ContainsKey(n.WorkItemId))
            .Select(n => new NotificationView(n.Id, n.Kind,
                n.ActorId is { } actor ? actors.GetValueOrDefault(actor)?.DisplayName : null,
                items[n.WorkItemId].ProjectKey, items[n.WorkItemId].Key, items[n.WorkItemId].Title, n.Detail, n.OccurredAt,
                n.ReadAt is not null))
            .ToList();
        return new Page<NotificationView>(views, total, request.Page, request.PageSize);
    }

    public async Task<Result> MarkAsync(long notificationId, bool read, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true })
        {
            return AppError.Forbidden();
        }

        if (await MineAsync(tracked: true, ct) is not { } mine
            || await mine.SingleOrDefaultAsync(n => n.Id == notificationId, ct) is not { } notification)
        {
            return AppError.NotFound("notification");
        }

        if (read)
        {
            notification.MarkRead(time.GetUtcNow());
        }
        else
        {
            notification.MarkUnread();
        }

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarkAllReadAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var now = time.GetUtcNow();
        await db.Notifications
            .Where(n => n.RecipientId == me.UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
        return Result.Ok();
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        // Finished emails are kept 30 days; their notifications' deletion takes any others with them (research R10).
        var now = time.GetUtcNow();
        var emailCutoff = now - EmailsKeptFor;
        await db.EmailMessages
            .Where(e => (e.SentAt ?? e.GivenUpAt ?? e.CancelledAt) < emailCutoff)
            .ExecuteDeleteAsync(ct);
        var cutoff = now - KeptFor;
        return await db.Notifications.Where(n => n.OccurredAt < cutoff).ExecuteDeleteAsync(ct);
    }

    /// <summary>The caller's notifications about projects they can still see (research R8); null when they can see
    /// none.</summary>
    private async Task<IQueryable<Notification>?> MineAsync(bool tracked, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return null;
        }

        var visible = await access.VisibleProjectIdsAsync(ct);
        if (visible is { Count: 0 })
        {
            return null;
        }

        var query = (tracked ? db.Notifications : db.Notifications.AsNoTracking()).Where(n => n.RecipientId == me.UserId);
        if (visible is not null)
        {
            var ids = visible.ToList();
            query = query.Where(n => ids.Contains(n.ProjectId));
        }

        return query;
    }
}
