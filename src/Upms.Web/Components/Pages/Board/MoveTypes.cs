using Upms.Application.Work;

namespace Upms.Web.Components.Pages.Board;

/// <summary>A column a card can be moved to.</summary>
public sealed record ColumnOption(long Id, string Name);

/// <summary>Another swimlane a card can be moved to, and what that changes (Jira parity pass FR-027).</summary>
public sealed record LaneOption(string Title, LaneChange Change);

/// <summary>Where the user asked to move a card; <paramref name="Lane"/> when it also changes lane.</summary>
public sealed record MoveRequest(long ColumnId, CardPlacement Placement, LaneChange? Lane = null);
