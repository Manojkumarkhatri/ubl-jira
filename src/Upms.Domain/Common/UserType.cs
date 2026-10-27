namespace Upms.Domain.Common;

/// <summary>The five kinds of user (owner's decision of 2026-10-20). The type sets the person's default ticks
/// (<see cref="Rights"/>); a space lead is a portfolio lead given single spaces. The numbers are stored; 0 is the default.</summary>
public enum UserType
{
    /// <summary>Works on the spaces they are in.</summary>
    Member = 0,

    /// <summary>Looks at the spaces they are in and downloads their MIS.</summary>
    ReadOnly = 1,

    /// <summary>Leads the portfolios or spaces given to them and reports on them.</summary>
    PortfolioLead = 2,

    /// <summary>Sees all of their department and reports on it.</summary>
    Head = 3,

    /// <summary>Everything, everywhere; sets everyone's type and ticks.</summary>
    Admin = 4,
}

public static class UserTypes
{
    /// <summary>The types from the most rights to the fewest, as lists show them.</summary>
    public static readonly UserType[] Ordered = [UserType.Admin, UserType.Head, UserType.PortfolioLead, UserType.Member, UserType.ReadOnly];

    /// <summary>The badge shown under a person's name.</summary>
    public static string DisplayName(this UserType type) => type switch
    {
        UserType.Admin => "Admin",
        UserType.Head => "Head",
        UserType.PortfolioLead => "Portfolio lead",
        UserType.ReadOnly => "Read-only",
        _ => "Member",
    };

    /// <summary>A short line saying what the type is for.</summary>
    public static string Description(this UserType type) => type switch
    {
        UserType.Admin => "Everything, everywhere. Sets everyone's type and ticks.",
        UserType.Head => "Sees all of their department and downloads its MIS.",
        UserType.PortfolioLead => "Leads the portfolios or spaces given to them.",
        UserType.ReadOnly => "Looks at the spaces they are in and downloads their MIS.",
        _ => "Works on items in the spaces they are in.",
    };

    /// <summary>Higher is more: people manage only those below their own type (Admins manage everyone).</summary>
    public static int Rank(this UserType type) => type switch
    {
        UserType.Admin => 4,
        UserType.Head => 3,
        UserType.PortfolioLead => 2,
        UserType.Member => 1,
        _ => 0,
    };

    /// <summary>For the CSS class of the badge, for example "type-portfoliolead".</summary>
    public static string CssName(this UserType type) => type.ToString().ToLowerInvariant();
}
