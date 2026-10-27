using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The actions of Jira's "⋯" menu on a work item: log work, vote, select a cover, clone and move.</summary>
public interface IWorkItemActionService
{
    /// <summary>The item's time tracking and its work logs, newest first (View right).</summary>
    Task<Result<TimeTracking>> GetTimeAsync(string workItemKey, CancellationToken ct);

    /// <summary>Contribute right. The time spent is Jira's "2w 3d 4h 30m"; the remaining estimate, when given, replaces
    /// the one worked out from the time logged (<c>InvalidDuration</c>).</summary>
    Task<Result<TimeTracking>> LogWorkAsync(string workItemKey, WorkLogInput input, CancellationToken ct);

    /// <summary>The author while they can contribute, Space admins and administrators.</summary>
    Task<Result<TimeTracking>> ChangeWorkLogAsync(long workLogId, WorkLogInput input, CancellationToken ct);

    /// <summary>The author while they can contribute, Space admins and administrators; the time goes back to the remaining
    /// estimate.</summary>
    Task<Result<TimeTracking>> DeleteWorkLogAsync(long workLogId, CancellationToken ct);

    /// <summary>Sets or clears the original estimate, written as "3d 4h"; until time is logged the remaining estimate
    /// follows it (Contribute right).</summary>
    Task<Result<TimeTracking>> SetOriginalEstimateAsync(string workItemKey, string? text, CancellationToken ct);

    /// <summary>Sets or clears the remaining estimate; "0" means nothing is left (Contribute right).</summary>
    Task<Result<TimeTracking>> SetRemainingEstimateAsync(string workItemKey, string? text, CancellationToken ct);

    /// <summary>Adds the caller's vote, or takes it back (View right, as in Jira).</summary>
    Task<Result<VoteState>> ToggleVoteAsync(string workItemKey, CancellationToken ct);

    /// <summary>The card's cover: one of <see cref="CoverColors.All"/>, an image attached to the item, or neither.</summary>
    Task<Result> SetCoverAsync(string workItemKey, string? color, long? attachmentId, CancellationToken ct);

    /// <summary>A copy in the same space and the first "to do" column, with the chosen parts; returns its key.</summary>
    Task<Result<string>> CloneAsync(string workItemKey, CloneRequest request, CancellationToken ct);

    /// <summary>What a move would do, for its confirmation step: the status it lands in and what it loses.</summary>
    Task<Result<MovePreview>> PreviewMoveAsync(string workItemKey, MoveTarget request, CancellationToken ct);

    /// <summary>Changes the item's type (epic, story, task, bug or sub-task), its parent or its space; returns its key,
    /// which changes when it moves to another space. Sub-tasks move with their parent.</summary>
    Task<Result<string>> MoveAsync(string workItemKey, MoveTarget request, CancellationToken ct);
}

/// <param name="TimeSpent">"2h 30m".</param>
/// <param name="Remaining">A new remaining estimate, or null to work it out.</param>
public sealed record WorkLogInput(string TimeSpent, DateTimeOffset StartedAt, string? Note, string? Remaining = null);

public sealed record TimeTracking(
    string WorkItemKey,
    int? OriginalMinutes,
    int? RemainingMinutes,
    int SpentMinutes,
    IReadOnlyList<WorkLogView> Logs,
    bool CanLog)
{
    /// <summary>The share of the bar that is logged: spent out of spent plus remaining (or the original estimate).</summary>
    public int Percent
    {
        get
        {
            var whole = SpentMinutes + (RemainingMinutes ?? Math.Max(0, (OriginalMinutes ?? 0) - SpentMinutes));
            return whole == 0 ? 0 : (int)Math.Round(100.0 * SpentMinutes / whole, MidpointRounding.AwayFromZero);
        }
    }
}

public sealed record WorkLogView(long Id, Guid AuthorId, string AuthorName, DateTimeOffset StartedAt, int Minutes, string? Note,
    DateTimeOffset CreatedAt, bool CanChange);

public sealed record VoteState(int Votes, bool HasVoted);

/// <param name="Title">The copy's summary; Jira starts it as "CLONE - …".</param>
/// <param name="ChildItems">Sub-tasks of a story, task or bug; the items of an epic.</param>
/// <param name="Links">The dependencies, "waits for" and "is waited for by".</param>
/// <param name="Sprint">The sprint, when it is active or planned.</param>
public sealed record CloneRequest(string Title, bool ChildItems, bool Attachments, bool Links, bool Sprint);

/// <param name="SpaceKey">The destination space; the item's own for a change of type or parent.</param>
/// <param name="ParentKey">For a sub-task, the story, task or bug it goes under.</param>
/// <param name="StatusId">A column of the destination space; by default one with the same name, else of the same type.</param>
public sealed record MoveTarget(string SpaceKey, WorkItemType Type, string? ParentKey = null, long? StatusId = null);

/// <param name="Consequences">What changes besides the type, space and parent, in short sentences.</param>
public sealed record MovePreview(
    string Key,
    string Title,
    string FromSpace,
    string ToSpace,
    WorkItemType FromType,
    WorkItemType ToType,
    string? FromParent,
    string? ToParent,
    string FromStatus,
    IReadOnlyList<StatusOption> Statuses,
    long StatusId,
    IReadOnlyList<string> Consequences);

/// <summary>The colours a card's cover can take, as Jira's "Select cover".</summary>
public static class CoverColors
{
    /// <summary>The hues of Jira's solid colours, each in four shades from light (1) to dark (4).</summary>
    public static readonly IReadOnlyList<string> Hues = ["gray", "blue", "sky", "teal", "lime", "yellow", "orange", "red", "magenta", "purple"];

    /// <summary>Row by row: every hue in shade 1, then shade 2, and so on, as the picker shows them.</summary>
    public static readonly IReadOnlyList<string> Solid = [.. Enumerable.Range(1, 4).SelectMany(shade => Hues.Select(hue => $"{hue}-{shade}"))];

    public static readonly IReadOnlyList<string> Gradients = [.. Enumerable.Range(1, 10).Select(n => $"gradient-{n}")];

    /// <summary>Every colour a cover can have; the first eight names are from before the picker had shades and still show.</summary>
    public static readonly IReadOnlyList<string> All =
        ["red", "orange", "yellow", "green", "teal", "blue", "purple", "grey", .. Solid, .. Gradients];
}
