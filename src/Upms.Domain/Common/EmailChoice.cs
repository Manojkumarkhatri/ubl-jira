namespace Upms.Domain.Common;

/// <summary>Which notifications a person also gets by email (Phase 4 FR-049, FR-051, research R11): none, only those aimed
/// at them (assignments, mentions, gates and blocked uploads), or all, which adds comments and status changes on items
/// they watch. Shared by the Identity module (the account keeps it) and the Notifications module (which applies it),
/// like <see cref="NotificationKind"/>.</summary>
public enum EmailChoice
{
    None,
    AimedAtMe,
    All,
}
