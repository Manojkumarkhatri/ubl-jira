using Upms.Domain.Common;

namespace Upms.Domain.Identity;

/// <summary>The default ticks of one user type, as an Admin set them on the rights page; one row per type, seeded with
/// <see cref="RightsCatalog.Defaults"/>.</summary>
public sealed class UserTypeRights
{
    private UserTypeRights()
    {
    }

    public UserTypeRights(UserType type, Rights rights)
    {
        Type = type;
        Rights = rights;
    }

    public UserType Type { get; private set; }

    public Rights Rights { get; private set; }

    /// <summary>An Admin keeps every right whatever is ticked; other types keep within their limit.</summary>
    public void Set(Rights rights) => Rights = Type == UserType.Admin ? Common.Rights.All : rights & RightsCatalog.Allowed(Type);
}
