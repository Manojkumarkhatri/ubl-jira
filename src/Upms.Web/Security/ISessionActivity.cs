namespace Upms.Web.Security;

/// <summary>Activity of the current sign-in session, shared by all of its tabs (FR-006).</summary>
public interface ISessionActivity
{
    /// <summary>When the user last did something in this session.</summary>
    DateTimeOffset LastActivity { get; }

    /// <summary>True once the session has ended because of inactivity.</summary>
    bool IsExpired { get; }

    /// <summary>Records activity now.</summary>
    void Touch();

    /// <summary>Ends the session because of inactivity.</summary>
    void Expire();
}
