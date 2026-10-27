using Upms.Domain.Common;

namespace Upms.Domain.Notifications;

/// <summary>Who hears about one action, and why (Phase 4 FR-017, research R8, R11).</summary>
public static class RecipientRules
{
    /// <summary>One reason per person, never the actor: a mention first, then an assignment, then a comment or status
    /// change on a watched item. Gate and upload events stand alone.</summary>
    public static IReadOnlyDictionary<Guid, NotificationKind> Resolve(
        IEnumerable<(NotificationKind Kind, IReadOnlyCollection<Guid> Recipients)> reasons, Guid? actorId)
    {
        var chosen = new Dictionary<Guid, NotificationKind>();
        foreach (var (kind, recipients) in reasons)
        {
            foreach (var recipient in recipients)
            {
                if (recipient == actorId)
                {
                    continue;
                }

                if (!chosen.TryGetValue(recipient, out var current) || Precedence(kind) < Precedence(current))
                {
                    chosen[recipient] = kind;
                }
            }
        }

        return chosen;
    }

    /// <summary>Whether a person with the email choice "only what is aimed at me" hears about it by email (FR-049).</summary>
    public static bool IsAimedAtRecipient(NotificationKind kind) => kind is not (NotificationKind.Commented or NotificationKind.StatusChanged);

    /// <summary>Whether a person with this email choice gets the notification by email too (FR-049, research R11).</summary>
    public static bool IsEmailed(NotificationKind kind, EmailChoice choice) => choice switch
    {
        EmailChoice.All => true,
        EmailChoice.AimedAtMe => IsAimedAtRecipient(kind),
        _ => false,
    };

    private static int Precedence(NotificationKind kind) => kind switch
    {
        NotificationKind.Mentioned => 0,
        NotificationKind.Assigned => 1,
        NotificationKind.Commented or NotificationKind.StatusChanged => 2,
        _ => 0,
    };
}
