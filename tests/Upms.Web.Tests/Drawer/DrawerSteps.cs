using Bunit;
using Upms.Web.Components.Pages.Drawer;

namespace Upms.Web.Tests.Drawer;

/// <summary>Steps of the drawer laid out as Jira's: the Activity section opens on the comments, and Archive is in the "⋯"
/// menu.</summary>
internal static class DrawerSteps
{
    /// <summary>Chooses an Activity tab: "All", "Comments", "History" or "Work log".</summary>
    public static IRenderedComponent<TaskDrawer> ShowActivity(this IRenderedComponent<TaskDrawer> cut, string tab)
    {
        cut.FindAll(".activity-tab").Single(t => t.TextContent.Trim() == tab).Click();
        return cut;
    }

    public static IRenderedComponent<TaskDrawer> ShowHistory(this IRenderedComponent<TaskDrawer> cut) => cut.ShowActivity("History");

    /// <summary>"⋯" › Archive, which asks for confirmation.</summary>
    public static void ChooseArchive(this IRenderedComponent<TaskDrawer> cut)
    {
        cut.Find("[data-testid=item-actions]").Click();
        cut.Find("[data-testid=item-action-delete]").Click();
    }
}
