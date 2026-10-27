namespace Upms.Web.Components.Pages.Board;

/// <summary>What a column's menu on the board can do (Jira parity pass FR-011).</summary>
public enum ColumnAction
{
    Rename,
    SetLimit,
    ClearLimit,
    MoveLeft,
    MoveRight,
    ChangeType,
    Delete,
}
