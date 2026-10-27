using System.Net;
using System.Text;
using Upms.Domain.Common;

namespace Upms.Application.Notifications;

/// <summary>What one email tells, read when it is sent, so names and titles are current (Phase 4 FR-050).</summary>
/// <param name="ItemKey">The item, or the stage for gate events.</param>
/// <param name="Detail">The new status, the gate's outcome or the blocked file's name.</param>
public sealed record EmailFacts(NotificationKind Kind, string? ActorName, string ProjectKey, string ProjectName, string ItemKey,
    string ItemTitle, string? Detail);

/// <summary>Builds an email's subject and body (Phase 4 FR-050, research R10): the project, the item's key and title (or the
/// stage and its gate), what happened, who did it and a link to the item. Never a description, comment text or attachment.
/// The HTML version is the same text, encoded.</summary>
public static class EmailComposer
{
    public static (string Subject, string Text, string Html) Compose(EmailFacts facts, string publicBaseUrl)
    {
        // Names, titles and file names come from people: each becomes one line, so none can add a header or a line of its
        // own (a second "Open it:" link, say) to the email.
        facts = facts with
        {
            ActorName = facts.ActorName is null ? null : OneLine(facts.ActorName),
            ProjectName = OneLine(facts.ProjectName),
            ItemTitle = OneLine(facts.ItemTitle),
            Detail = facts.Detail is null ? null : OneLine(facts.Detail),
        };
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var subject = IsGate(facts.Kind) ? $"[{facts.ProjectKey}] {facts.ItemTitle} gate" : $"[{facts.ItemKey}] {facts.ItemTitle}";
        var what = Sentence(facts);
        var project = $"{facts.ProjectName} ({facts.ProjectKey})";
        var link = $"{baseUrl}/spaces/{Uri.EscapeDataString(facts.ProjectKey)}/board?task={Uri.EscapeDataString(facts.ItemKey)}";
        var profile = $"{baseUrl}/account/profile";

        var text = $"""
            {what}.

            Space: {project}
            Open it: {link}

            You get this email because of your email choice. You can change it in your profile: {profile}
            """;
        var html = $"""
            <p>{Encode(what)}.</p>
            <p>Space: {Encode(project)}<br><a href="{Encode(link)}">Open {Encode(facts.ItemKey)} in U-PMS</a></p>
            <p style="color:#555">You get this email because of your email choice. You can change it in
            <a href="{Encode(profile)}">your profile</a>.</p>
            """;
        return (subject, text.ReplaceLineEndings("\r\n"), html.ReplaceLineEndings("\r\n"));
    }

    /// <summary>What happened, addressed to the recipient (contracts/ui-routes.md, "Screen text").</summary>
    public static string Sentence(EmailFacts f) => f.Kind switch
    {
        NotificationKind.Mentioned => $"{f.ActorName} mentioned you on {f.ItemKey} {f.ItemTitle}",
        NotificationKind.Assigned => $"{f.ActorName} assigned {f.ItemKey} {f.ItemTitle} to you",
        NotificationKind.Commented => $"{f.ActorName} commented on {f.ItemKey} {f.ItemTitle}",
        NotificationKind.StatusChanged => $"{f.ActorName} moved {f.ItemKey} {f.ItemTitle} to {f.Detail}",
        NotificationKind.ApprovalRequested => $"{f.ActorName} requested your approval of the {f.ItemTitle} gate in {f.ProjectKey}",
        NotificationKind.GateDecided => $"The {f.ItemTitle} gate in {f.ProjectKey} was {(f.Detail == "Approved" ? "approved" : "sent back")}",
        NotificationKind.UploadBlocked => $"{f.Detail} on {f.ItemKey} was blocked by the malware check",
        _ => $"{f.ItemKey} {f.ItemTitle}",
    };

    private static bool IsGate(NotificationKind kind) => kind is NotificationKind.ApprovalRequested or NotificationKind.GateDecided;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    /// <summary>Control characters and line breaks become single spaces.</summary>
    private static string OneLine(string text)
    {
        var line = new StringBuilder(text.Length);
        var gap = false;
        foreach (var c in text)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                gap = line.Length > 0;
                continue;
            }

            if (gap)
            {
                line.Append(' ');
                gap = false;
            }

            line.Append(c);
        }

        return line.ToString();
    }
}
