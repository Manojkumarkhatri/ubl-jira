namespace Upms.Web.Components.Layout;

/// <summary>Opens the Create form from anywhere (the header's Create button), and tells pages when it created an item so
/// that a board or list of the same space can show it (Jira parity pass FR-001; research R10).</summary>
public sealed class CreateWorkLauncher
{
    /// <summary>Asks the dialog to open, on the given space or on the space of the current page, and in the given status (a
    /// board column's "+ Create") or the first "to do" one, and with a due date (a calendar day's "+").</summary>
    public event Action<string?, long?, DateOnly?>? Requested;

    /// <summary>An item was created: its space's key and its own key.</summary>
    public event Action<string, string>? Created;

    public void Open(string? spaceKey = null, long? statusId = null, DateOnly? due = null) => Requested?.Invoke(spaceKey, statusId, due);

    public void NotifyCreated(string spaceKey, string itemKey) => Created?.Invoke(spaceKey, itemKey);

    /// <summary>The space key of a space page's address, such as WEB for <c>spaces/WEB/board?task=WEB-1</c>.</summary>
    public static string? SpaceKeyOf(string relativeAddress)
    {
        var path = relativeAddress.Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return path.Length >= 2 && path[0].Equals("spaces", StringComparison.OrdinalIgnoreCase) ? path[1].ToUpperInvariant() : null;
    }
}
