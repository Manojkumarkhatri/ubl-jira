using Upms.Application.Identity;
using Upms.Application.Projects;

namespace Upms.Web.Components.Layout;

/// <summary>The sidebar's state for one person's session (Jira parity pass FR-021, FR-021b; research R5, R7): whether it is
/// collapsed, whether the phone panel is open, and the starred and recent spaces, which star buttons and space pages
/// change.</summary>
public sealed class SidebarState(IAccountService accounts, ISpaceNavigationService navigation)
{
    private bool _loadedChoice;
    private Task? _loading;

    /// <summary>Something the sidebar shows changed.</summary>
    public event Action? Changed;

    /// <summary>The person keeps the sidebar collapsed (wide screens).</summary>
    public bool Collapsed { get; private set; }

    /// <summary>The phone panel is open (narrow screens).</summary>
    public bool PanelOpen { get; private set; }

    /// <summary>Starred and recent spaces, or null before the first load.</summary>
    public SidebarView? View { get; private set; }

    /// <summary>Reads the person's choice once per session, so the first page already has the right layout.</summary>
    public async Task EnsureChoiceAsync()
    {
        if (_loadedChoice)
        {
            return;
        }

        _loadedChoice = true;
        if (await accounts.GetProfileAsync(CancellationToken.None) is { IsSuccess: true } profile)
        {
            Collapsed = profile.Value!.SidebarCollapsed;
        }
    }

    /// <summary>Loads the spaces once for everything on the page that shows them (the sidebar, star buttons).</summary>
    public Task EnsureLoadedAsync() => View is not null ? Task.CompletedTask : _loading ??= ReloadAsync();

    public async Task ReloadAsync()
    {
        if (await navigation.GetSidebarAsync(CancellationToken.None) is { IsSuccess: true } view)
        {
            View = view.Value!;
            Changed?.Invoke();
        }
    }

    public bool IsStarred(string spaceKey) => View?.Starred.Any(s => s.Key.Equals(spaceKey, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>Stars or unstars a space and shows it at once in the sidebar.</summary>
    public async Task<bool> ToggleStarAsync(string spaceKey)
    {
        var starring = !IsStarred(spaceKey);
        var done = starring
            ? await navigation.StarAsync(spaceKey, CancellationToken.None)
            : await navigation.UnstarAsync(spaceKey, CancellationToken.None);
        await ReloadAsync();
        return done.IsSuccess && starring;
    }

    /// <summary>A space page opened: it moves to the top of the person's recent spaces.</summary>
    public async Task VisitedAsync(string spaceKey)
    {
        if ((await navigation.RecordVisitAsync(spaceKey, CancellationToken.None)).IsSuccess)
        {
            await ReloadAsync();
        }
    }

    /// <summary>The wide-screen button: collapses or opens the sidebar and keeps the choice on the person.</summary>
    public async Task ToggleCollapsedAsync()
    {
        Collapsed = !Collapsed;
        Changed?.Invoke();
        await accounts.SetSidebarCollapsedAsync(Collapsed, CancellationToken.None);
    }

    public void SetPanel(bool open)
    {
        if (PanelOpen != open)
        {
            PanelOpen = open;
            Changed?.Invoke();
        }
    }
}
