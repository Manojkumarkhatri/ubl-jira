namespace Upms.Domain.Identity;

/// <summary>Organization-wide role (FR-008).</summary>
public enum OrganizationRole
{
    /// <summary>A regular user.</summary>
    User,

    /// <summary>Manages accounts and has full rights in every project.</summary>
    Administrator,
}
