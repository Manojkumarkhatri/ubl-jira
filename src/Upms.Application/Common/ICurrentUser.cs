namespace Upms.Application.Common;

/// <summary>Who is calling. The caller's identity always comes from here, never from a parameter
/// (contracts/application-services.md).</summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's ID, or null for an anonymous caller.</summary>
    Guid? UserId { get; }
}
