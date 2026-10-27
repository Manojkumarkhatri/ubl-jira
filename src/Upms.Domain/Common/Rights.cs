namespace Upms.Domain.Common;

/// <summary>The tick boxes: what a person may do (owner's decision of 2026-10-20). Each user type has default ticks, and a
/// person can have ticks of their own on top; an Admin always has every one. A type also has a limit: the ticks it can
/// never have, shown locked (owner, 2026-10-26). The numbers are stored as a bit set.</summary>
[Flags]
public enum Rights : long
{
    None = 0,

    /// <summary>Create, edit, move and comment on work items in the spaces they are in (a space's Member).</summary>
    EditWork = 1 << 0,

    /// <summary>Approve or send back a stage gate they are an approver of.</summary>
    ApproveGates = 1 << 1,

    /// <summary>Change the settings of the spaces they are in: details, columns, fields, work types and members (a space's
    /// admin).</summary>
    ManageSpaces = 1 << 2,

    DownloadSpaceMis = 1 << 3,

    DownloadPortfolioMis = 1 << 4,

    DownloadDepartmentMis = 1 << 5,

    DownloadAllMis = 1 << 6,

    /// <summary>Export work items, the timeline and documents (Excel, Word, PDF, images).</summary>
    Export = 1 << 7,

    CreateSpaces = 1 << 8,

    /// <summary>Add Member and Read-only users; outside the Admins, only to their own department.</summary>
    AddPeople = 1 << 9,

    /// <summary>Change the type and ticks of people below their own type; outside the Admins, only in their own
    /// department.</summary>
    SetAccess = 1 << 10,

    /// <summary>Add Portfolio lead users.</summary>
    AddPortfolioLeads = 1 << 11,

    /// <summary>Add Head users.</summary>
    AddHeads = 1 << 12,

    /// <summary>Delete the work items they created, in the spaces where they can edit (owner, 2026-10-26).</summary>
    DeleteOwnWork = 1 << 13,

    /// <summary>Delete any work item in the spaces where they can edit (Jira's "Delete work items").</summary>
    DeleteAnyWork = 1 << 14,

    /// <summary>Move a space to the trash: an Admin anywhere, a Head in their department, a Portfolio lead in the spaces
    /// they manage (owner, 2026-10-26).</summary>
    DeleteSpaces = 1 << 15,

    /// <summary>Reset passwords, deactivate and reactivate people below their own type.</summary>
    ManageAccounts = 1 << 16,

    All = EditWork | ApproveGates | ManageSpaces | DownloadSpaceMis | DownloadPortfolioMis | DownloadDepartmentMis | DownloadAllMis
        | Export | CreateSpaces | AddPeople | SetAccess | AddPortfolioLeads | AddHeads | DeleteOwnWork | DeleteAnyWork | DeleteSpaces
        | ManageAccounts,
}

/// <summary>A tick box as the user pages list it.</summary>
public sealed record RightInfo(Rights Right, string Name, string Group, string Description);

public static class RightsCatalog
{
    /// <summary>Every tick box, in the order and groups the pages show.</summary>
    public static readonly IReadOnlyList<RightInfo> All =
    [
        new(Rights.EditWork, "Create and edit work items", "Work items", "Create, edit, move and comment on work items in their spaces."),
        new(Rights.DeleteOwnWork, "Delete work items they created", "Work items", "Epics, tasks and sub-tasks they created. Deleted items go to Archived work items."),
        new(Rights.DeleteAnyWork, "Delete any work item", "Work items", "Any epic, task or sub-task in the spaces where they can edit."),
        new(Rights.ApproveGates, "Approve stage gates", "Work items", "Approve or send back a gate they are named on."),
        new(Rights.CreateSpaces, "Create spaces", "Spaces", "They become the new space's admin."),
        new(Rights.ManageSpaces, "Manage space settings and members", "Spaces", "Details, columns, fields, work types and members of their spaces."),
        new(Rights.DeleteSpaces, "Delete spaces", "Spaces", "Moves a space to the trash, where an Admin can restore it. A Head: their department's spaces."),
        new(Rights.DownloadSpaceMis, "Download space MIS", "MIS and exports", "The MIS of the spaces they are in."),
        new(Rights.DownloadPortfolioMis, "Download portfolio MIS", "MIS and exports", "The MIS of the portfolios they lead."),
        new(Rights.DownloadDepartmentMis, "Download department MIS", "MIS and exports", "The MIS of their whole department."),
        new(Rights.DownloadAllMis, "Download all departments MIS", "MIS and exports", "The MIS of every department."),
        new(Rights.Export, "Export Excel, Word, PDF and images", "MIS and exports", "Work items, the timeline and documents."),
        new(Rights.AddPeople, "Add Member and Read-only users", "Users", "In their own department."),
        new(Rights.AddPortfolioLeads, "Add Portfolio lead users", "Users", "In their own department."),
        new(Rights.AddHeads, "Add Head users", "Users", "Heads of any department."),
        new(Rights.SetAccess, "Change users' type and rights", "Users", "People below their own type, in their own department."),
        new(Rights.ManageAccounts, "Reset passwords and deactivate users", "Users", "People below their own type, in their own department."),
    ];

    /// <summary>The ticks a type can have at all; the others show locked (owner, 2026-10-26). A Member can never add a
    /// Portfolio lead, a Head never sees other departments' MIS.</summary>
    public static Rights Allowed(UserType type) => type switch
    {
        UserType.Admin => Rights.All,
        UserType.Head => Rights.All & ~(Rights.DownloadAllMis | Rights.AddHeads),
        UserType.PortfolioLead => Rights.EditWork | Rights.DeleteOwnWork | Rights.DeleteAnyWork | Rights.ApproveGates | Rights.CreateSpaces
            | Rights.ManageSpaces | Rights.DeleteSpaces | Rights.DownloadSpaceMis | Rights.DownloadPortfolioMis | Rights.Export
            | Rights.AddPeople | Rights.SetAccess,
        UserType.ReadOnly => Rights.DownloadSpaceMis | Rights.Export,
        _ => Rights.EditWork | Rights.DeleteOwnWork | Rights.DeleteAnyWork | Rights.ApproveGates | Rights.DownloadSpaceMis | Rights.Export,
    };

    /// <summary>The ticks each type starts with (the owner's matrix), until an Admin changes them. Deleting: Admins and Heads
    /// delete spaces; Admins, Heads and Portfolio leads delete any work item; Members their own (owner, 2026-10-26).</summary>
    public static Rights Defaults(UserType type) => type switch
    {
        UserType.Admin => Rights.All,
        UserType.Head => Allowed(UserType.Head),
        UserType.PortfolioLead => Rights.EditWork | Rights.DeleteOwnWork | Rights.DeleteAnyWork | Rights.ApproveGates | Rights.ManageSpaces
            | Rights.DownloadSpaceMis | Rights.DownloadPortfolioMis | Rights.Export | Rights.CreateSpaces,
        UserType.ReadOnly => Rights.DownloadSpaceMis,
        _ => Rights.EditWork | Rights.DeleteOwnWork | Rights.DownloadSpaceMis | Rights.Export,
    };

    /// <summary>What a person may do: their type's ticks, plus the ones given to them, less the ones taken away, within the
    /// type's limit. An Admin keeps every right.</summary>
    public static Rights Effective(UserType type, Rights typeDefaults, Rights granted, Rights denied) =>
        type == UserType.Admin ? Rights.All : ((typeDefaults | granted) & ~denied) & Allowed(type);

    /// <summary>The tick needed to add, or give, a type: Admins only add Admins.</summary>
    public static Rights AddRightFor(UserType type) => type switch
    {
        UserType.Head => Rights.AddHeads,
        UserType.PortfolioLead => Rights.AddPortfolioLeads,
        UserType.Member or UserType.ReadOnly => Rights.AddPeople,
        _ => Rights.None,
    };

    /// <summary>Any of the ticks that open the User management page.</summary>
    public const Rights ManagesUsers = Rights.AddPeople | Rights.AddPortfolioLeads | Rights.AddHeads | Rights.SetAccess | Rights.ManageAccounts;

    public static bool Has(this Rights rights, Rights right) => right != Rights.None && (rights & right) == right;

    public static bool HasAny(this Rights rights, Rights any) => (rights & any) != Rights.None;
}
