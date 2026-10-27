using Upms.Application.Common.Results;
using Upms.Application.Work;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeWatcherService(Guid me) : IWatcherService
{
    public List<PersonView> Watching { get; } = [];

    public bool CanManage { get; set; }

    public List<string> Calls { get; } = [];

    public PersonView Me { get; } = new(me, "Amina Khan", "amina");

    public Task<Result<WatchersView>> GetAsync(string workItemKey, CancellationToken ct) => Answer("get");

    public Task<Result<WatchersView>> WatchAsync(string workItemKey, CancellationToken ct)
    {
        if (Watching.All(p => p.UserId != me))
        {
            Watching.Add(Me);
        }

        return Answer("watch");
    }

    public Task<Result<WatchersView>> StopWatchingAsync(string workItemKey, CancellationToken ct)
    {
        Watching.RemoveAll(p => p.UserId == me);
        return Answer("stop");
    }

    public Task<Result<WatchersView>> AddAsync(string workItemKey, Guid userId, CancellationToken ct)
    {
        Watching.Add(new PersonView(userId, $"Person {Watching.Count}", "person"));
        return Answer($"add {userId}");
    }

    public Task<Result<WatchersView>> RemoveAsync(string workItemKey, Guid userId, CancellationToken ct)
    {
        Watching.RemoveAll(p => p.UserId == userId);
        return Answer($"remove {userId}");
    }

    public WatchersView View() => new(Watching.Count, Watching.Any(p => p.UserId == me), Watching.ToList(), CanManage);

    private Task<Result<WatchersView>> Answer(string call)
    {
        Calls.Add(call);
        return Task.FromResult(Result<WatchersView>.Ok(View()));
    }
}
