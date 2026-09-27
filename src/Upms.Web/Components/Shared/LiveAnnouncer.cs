namespace Upms.Web.Components.Shared;

/// <summary>Sends messages to the page's polite live region and toast area, so that moves and saves are
/// announced to screen readers and shown visibly (FR-042, constitution VI).</summary>
public sealed class LiveAnnouncer
{
    public event Action<string>? Announced;

    public void Announce(string message) => Announced?.Invoke(message);
}
