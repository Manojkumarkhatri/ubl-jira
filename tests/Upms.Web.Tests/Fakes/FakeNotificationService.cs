using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Notifications;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeNotificationService : INotificationService
{
    public List<NotificationView> Items { get; } = [];

    public int Counts { get; private set; }

    public List<(long Id, bool Read)> Marks { get; } = [];

    public Task<int> CountUnreadAsync(CancellationToken ct)
    {
        Counts++;
        return Task.FromResult(Items.Count(n => !n.IsRead));
    }

    public Task<Result<Page<NotificationView>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var items = Items.OrderByDescending(n => n.OccurredAt).Skip((page.Page - 1) * 20).Take(20).ToList();
        return Task.FromResult(Result<Page<NotificationView>>.Ok(new Page<NotificationView>(items, Items.Count, page.Page, 20)));
    }

    public Task<Result> MarkAsync(long notificationId, bool read, CancellationToken ct)
    {
        Marks.Add((notificationId, read));
        var index = Items.FindIndex(n => n.Id == notificationId);
        Items[index] = Items[index] with { IsRead = read };
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> MarkAllReadAsync(CancellationToken ct)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i] = Items[i] with { IsRead = true };
        }

        return Task.FromResult(Result.Ok());
    }
}
