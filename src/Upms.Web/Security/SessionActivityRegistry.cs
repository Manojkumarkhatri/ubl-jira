using System.Collections.Concurrent;

namespace Upms.Web.Security;

/// <summary>Last activity per sign-in session (the <c>upms:sid</c> claim), shared by all tabs and by the
/// keep-alive endpoint (FR-006). In memory: Phase 1 runs a single app instance.</summary>
public sealed class SessionActivityRegistry(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);

    public DateTimeOffset GetLastActivity(string sessionId) =>
        _sessions.GetOrAdd(sessionId, _ => new Entry(time.GetUtcNow(), false)).LastActivity;

    public bool IsExpired(string sessionId) => _sessions.TryGetValue(sessionId, out var entry) && entry.Expired;

    public void Touch(string sessionId)
    {
        var now = time.GetUtcNow();
        _sessions.AddOrUpdate(sessionId, _ => new Entry(now, false), (_, e) => e.Expired ? e : e with { LastActivity = now });
        Prune(now);
    }

    public void Expire(string sessionId) =>
        _sessions.AddOrUpdate(sessionId, _ => new Entry(time.GetUtcNow(), true), (_, e) => e with { Expired = true });

    private void Prune(DateTimeOffset now)
    {
        if (_sessions.Count < 1000)
        {
            return;
        }

        foreach (var (key, entry) in _sessions)
        {
            if (now - entry.LastActivity > TimeSpan.FromHours(12))
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }

    private sealed record Entry(DateTimeOffset LastActivity, bool Expired);
}
