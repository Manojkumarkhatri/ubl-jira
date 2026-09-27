namespace Upms.Application.Common;

internal sealed class CallerContext(ICurrentUser currentUser, IUserStatusReader statusReader) : ICallerContext
{
    public async Task<CallerStatus?> GetAsync(CancellationToken ct) =>
        currentUser.UserId is { } userId ? await statusReader.GetAsync(userId, ct) : null;
}
