using System.Collections.Concurrent;
using Upms.Application.Notifications;

namespace Upms.Application.Tests.Fixtures;

/// <summary>A mail server for tests (Phase 4 research R10): keeps what it is sent; while <see cref="Down"/>, every send
/// fails as an unreachable server would.</summary>
public sealed class FakeEmailTransport : IEmailTransport
{
    private readonly ConcurrentQueue<OutgoingEmail> _sent = new();
    private int _tries;

    public bool IsConfigured { get; set; } = true;

    public bool Down { get; set; }

    /// <summary>How long each send takes, to make two workers overlap.</summary>
    public TimeSpan Delay { get; set; }

    public IReadOnlyList<OutgoingEmail> Sent => [.. _sent];

    public int Tries => _tries;

    public async Task SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        Interlocked.Increment(ref _tries);
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, ct);
        }

        if (Down)
        {
            throw new IOException("Connection refused");
        }

        _sent.Enqueue(email);
    }
}
