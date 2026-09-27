using Upms.Application.Work;

namespace Upms.Web.Components.Pages.Board;

/// <summary>A column a card can be moved to.</summary>
public sealed record ColumnOption(long Id, string Name);

/// <summary>Where the user asked to move a card.</summary>
public sealed record MoveRequest(long ColumnId, CardPlacement Placement);
