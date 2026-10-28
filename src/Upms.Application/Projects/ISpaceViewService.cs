using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Projects;

/// <summary>A space's tabs and shortcuts, as Jira's space navigation: everyone sees them; Space admins choose which views
/// are tabs ("Add to navigation") and pin shortcuts.</summary>
public interface ISpaceViewService
{
    Task<Result<SpaceViews>> GetAsync(string spaceKey, CancellationToken ct);

    /// <summary>The tabs in order: views the template has, at least one (Space admins).</summary>
    Task<Result<SpaceViews>> SetViewsAsync(string spaceKey, IReadOnlyList<string> views, CancellationToken ct);

    /// <summary>Pins a link (Space admins): a name and an http or https address.</summary>
    Task<Result<SpaceViews>> AddShortcutAsync(string spaceKey, string name, string url, CancellationToken ct);

    Task<Result<SpaceViews>> RemoveShortcutAsync(string spaceKey, long shortcutId, CancellationToken ct);
}

/// <param name="Views">The tabs, in order.</param>
/// <param name="Available">Every view the space's template has, for the "+" menu.</param>
public sealed record SpaceViews(IReadOnlyList<string> Views, IReadOnlyList<string> Available, bool CanManage, IReadOnlyList<ShortcutView> Shortcuts)
{
    /// <summary>What a space page's header needs: the name and template, and what the caller may do.</summary>
    public string SpaceName { get; init; } = "";

    public ProjectTemplate Template { get; init; }

    public bool CanContribute { get; init; }

    /// <summary>Administrators restore archived work items.</summary>
    public bool IsAdministrator { get; init; }
}

public sealed record ShortcutView(long Id, string Name, string Url);

/// <summary>The views a space can show as tabs, by their address names.</summary>
public static class SpaceViewNames
{
    public const string Summary = "summary";
    public const string Backlog = "backlog";
    public const string Board = "board";
    public const string List = "list";
    public const string Calendar = "calendar";
    public const string Timeline = "timeline";
    public const string Stages = "stages";
    public const string Approvals = "approvals";
    public const string Forms = "forms";
    public const string Docs = "docs";
    public const string Attachments = "attachments";
    public const string Reports = "reports";
    public const string Archived = "archived";

    /// <summary>The template's views, in Jira's order.</summary>
    public static IReadOnlyList<string> AvailableFor(ProjectTemplate template) =>
    [
        Summary,
        .. template == ProjectTemplate.Scrum ? [Backlog] : Array.Empty<string>(),
        Board, List, Calendar, Timeline,
        // Approvals come with Stage-gate gates; elsewhere the tab only pointed to a Stage-gate space (live test, 2026-10-28;
        // approvals in other spaces wait for the owner's decision).
        .. template == ProjectTemplate.StageGate ? [Stages, Approvals] : Array.Empty<string>(),
        Forms, Docs, Attachments, Reports, Archived,
    ];

    /// <summary>The tabs of a space that has not chosen its own: every view.</summary>
    public static IReadOnlyList<string> DefaultsFor(ProjectTemplate template) => AvailableFor(template);
}
