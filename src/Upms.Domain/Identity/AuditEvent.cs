namespace Upms.Domain.Identity;

/// <summary>A security-relevant event (FR-010). The table is append-only (database trigger).</summary>
public sealed class AuditEvent
{
    public const int TargetMaxLength = 200;
    public const int DetailsMaxLength = 2000;
    public const int SourceIpMaxLength = 45;

    private AuditEvent()
    {
    }

    public AuditEvent(
        AuditEventType eventType,
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        Guid? subjectUserId,
        string target,
        string? details,
        string? sourceIp)
    {
        EventType = eventType;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        SubjectUserId = subjectUserId;
        Target = Truncate(target, TargetMaxLength) ?? "";
        Details = Truncate(details, DetailsMaxLength);
        SourceIp = Truncate(sourceIp, SourceIpMaxLength);
    }

    public long Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditEventType EventType { get; private set; }

    /// <summary>Who acted; null for a failed sign-in with an unknown user name.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>The account affected.</summary>
    public Guid? SubjectUserId { get; private set; }

    /// <summary>For example the user name that was tried.</summary>
    public string Target { get; private set; } = "";

    /// <summary>JSON details; never passwords or tokens.</summary>
    public string? Details { get; private set; }

    public string? SourceIp { get; private set; }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
