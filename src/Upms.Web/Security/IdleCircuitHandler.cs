using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Upms.Web.Security;

/// <summary>Counts opening or reconnecting a page as activity (FR-006). User input inside the page is
/// reported by <c>idle-monitor.js</c> through the keep-alive endpoint, because the circuit's own traffic
/// (render acknowledgements) is not user activity.</summary>
internal sealed class IdleCircuitHandler(ISessionActivity activity) : CircuitHandler
{
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!activity.IsExpired)
        {
            activity.Touch();
        }

        return Task.CompletedTask;
    }
}
