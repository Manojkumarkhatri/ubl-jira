using Upms.Web.Security;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeSessionActivity(TimeProvider time) : ISessionActivity
{
    public DateTimeOffset LastActivity { get; private set; } = time.GetUtcNow();

    public bool IsExpired { get; private set; }

    public int TouchCount { get; private set; }

    public void Touch()
    {
        LastActivity = time.GetUtcNow();
        TouchCount++;
    }

    public void Expire() => IsExpired = true;
}
