using Upms.Application.Notifications;
using Upms.Domain.Common;

namespace Upms.Web.Components.Layout;

/// <summary>What each notification says (Phase 4 contracts/ui-routes.md, "Screen text").</summary>
public static class NotificationText
{
    public static string Sentence(NotificationView n) => n.Kind switch
    {
        NotificationKind.Mentioned => $"{n.ActorName} mentioned you on {n.ItemKey} {n.ItemTitle}",
        NotificationKind.Assigned => $"{n.ActorName} assigned {n.ItemKey} {n.ItemTitle} to you",
        NotificationKind.Commented => $"{n.ActorName} commented on {n.ItemKey} {n.ItemTitle}",
        NotificationKind.StatusChanged => $"{n.ActorName} moved {n.ItemKey} {n.ItemTitle} to {n.Detail}",
        NotificationKind.ApprovalRequested => $"{n.ActorName} requested approval of the {n.ItemTitle} gate in {n.ProjectKey}",
        NotificationKind.GateDecided =>
            $"The {n.ItemTitle} gate in {n.ProjectKey} was {(n.Detail == "Approved" ? "approved" : "sent back")}",
        NotificationKind.UploadBlocked => $"{n.Detail} on {n.ItemKey} was blocked by the malware check",
        _ => $"{n.ItemKey} {n.ItemTitle}",
    };

    /// <summary>The item's drawer over its project's board.</summary>
    public static string Link(NotificationView n) => $"spaces/{n.ProjectKey}/board?task={Uri.EscapeDataString(n.ItemKey)}";
}
