using Microsoft.AspNetCore.Components;

namespace Upms.Web.Tests;

/// <summary>Applies address changes when told to, as a browser does after a round trip.</summary>
public sealed class SlowNavigationManager : NavigationManager
{
    private readonly Queue<string> _pending = new();

    public SlowNavigationManager(string path) => Initialize("http://localhost/", "http://localhost/" + path);

    public bool Holding { get; set; }

    public void Arrive()
    {
        while (_pending.TryDequeue(out var uri))
        {
            Uri = uri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        _pending.Enqueue(ToAbsoluteUri(uri).ToString());
        if (!Holding)
        {
            Arrive();
        }
    }
}
