namespace Upms.Domain.Identity;

/// <summary>Organization-wide settings; a single row with <see cref="SingletonId"/>.</summary>
public sealed class OrganizationSettings
{
    public const int SingletonId = 1;
    public const string DefaultTimeZone = "UTC";
    public const int DefaultIdleTimeoutMinutes = 30;

    public int Id { get; private set; } = SingletonId;

    /// <summary>An IANA time zone ID; seeded "UTC".</summary>
    public string DefaultTimeZoneId { get; private set; } = DefaultTimeZone;

    /// <summary>Idle timeout for sessions (FR-006); seeded 30, editable in a later phase.</summary>
    public int IdleTimeoutMinutes { get; private set; } = DefaultIdleTimeoutMinutes;

    /// <summary>Set by first-run setup; <c>/setup</c> closes once it is set (FR-002).</summary>
    public DateTimeOffset? SetupCompletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];
}
