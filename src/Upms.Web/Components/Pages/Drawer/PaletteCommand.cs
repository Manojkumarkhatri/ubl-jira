namespace Upms.Web.Components.Pages.Drawer;

/// <summary>One action of the command palette, under a group heading such as "Status" or "Actions".</summary>
public sealed record PaletteCommand(string Group, string Label, Func<Task> Run, string? Hint = null);
