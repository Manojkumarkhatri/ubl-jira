namespace Upms.Application.Common.Results;

/// <summary>Stable error codes (contracts/application-services.md, "Rule violation codes").</summary>
public static class ErrorCodes
{
    public const string NotFound = "NotFound";
    public const string Forbidden = "Forbidden";
    public const string Validation = "Validation";
    public const string Conflict = "Conflict";

    public const string SetupClosed = "SetupClosed";
    public const string InvalidSetupToken = "InvalidSetupToken";
    public const string DuplicateUserName = "DuplicateUserName";
    public const string DuplicateEmail = "DuplicateEmail";
    public const string LastAdministrator = "LastAdministrator";
    public const string AccountDeactivated = "AccountDeactivated";
    public const string DuplicateMember = "DuplicateMember";
    public const string LastProjectAdmin = "LastProjectAdmin";
    public const string DuplicateProjectKey = "DuplicateProjectKey";
    public const string DuplicateProjectName = "DuplicateProjectName";
    public const string InvalidProjectKey = "InvalidProjectKey";
    public const string DuplicateColumnName = "DuplicateColumnName";
    public const string TooManyColumns = "TooManyColumns";
    public const string LastToDoColumn = "LastToDoColumn";
    public const string LastDoneColumn = "LastDoneColumn";
    public const string ColumnNotEmpty = "ColumnNotEmpty";
    public const string DestinationRequired = "DestinationRequired";
    public const string SubtaskDepth = "SubtaskDepth";
    public const string CommentNotOwned = "CommentNotOwned";
    public const string NotAssignable = "NotAssignable";
    public const string InvalidDates = "InvalidDates";

    // Phase 3 (specs/004-templates-and-planning/contracts/application-services.md)
    public const string TypeNotAllowed = "TypeNotAllowed";
    public const string TypeChangeNotAllowed = "TypeChangeNotAllowed";
    public const string InvalidContainer = "InvalidContainer";
    public const string NoSubtasks = "NoSubtasks";
    public const string TemplateSwitchNotAllowed = "TemplateSwitchNotAllowed";
    public const string SprintsOpen = "SprintsOpen";
    public const string SprintAlreadyActive = Domain.Work.Sprint.AlreadyActiveCode;
    public const string SprintCompleted = Domain.Work.Sprint.CompletedCode;
    public const string SprintNotActive = Domain.Work.Sprint.NotActiveCode;
    public const string SprintNotPlanned = Domain.Work.Sprint.NotPlannedCode;
    public const string StageLocked = Domain.Work.StageGate.StageLockedCode;
    public const string GateNotApproved = Domain.Work.StageGate.NotApprovedCode;
    public const string GateApproved = Domain.Work.StageGate.ApprovedCode;
    public const string GateRequestPending = Domain.Work.StageGate.RequestPendingCode;
    public const string NoApprovers = Domain.Work.StageGate.NoApproversCode;
    public const string ApproverCannotDecide = Domain.Work.StageGate.ApproverCannotDecideCode;
    public const string NotEnoughApprovers = Domain.Work.StageGate.NotEnoughApproversCode;
    public const string AlreadyDecided = Domain.Work.StageGate.AlreadyDecidedCode;
    public const string StageOrderFixed = Domain.Work.StageOrder.FixedCode;
    public const string DependencyToSelf = Domain.Work.DependencyGraph.ToSelfCode;
    public const string DuplicateDependency = Domain.Work.DependencyGraph.DuplicateCode;
    public const string DependencyInHierarchy = Domain.Work.DependencyGraph.InHierarchyCode;
    public const string DependencyLoop = Domain.Work.DependencyGraph.LoopCode;

    // Phase 4 (specs/005-collaboration-and-search/contracts/application-services.md)
    public const string UnsupportedFormatting = Domain.Work.FormattedText.UnsupportedFormattingCode;
    public const string InvalidLink = Domain.Work.FormattedText.InvalidLinkCode;
    public const string TextTooLong = Domain.Work.FormattedText.TextTooLongCode;
    public const string NotAMember = "NotAMember";
    public const string FileTypeNotAllowed = Domain.Work.AttachmentRules.FileTypeNotAllowedCode;
    public const string FileTooLarge = Domain.Work.AttachmentRules.FileTooLargeCode;
    public const string FileContentMismatch = Domain.Work.AttachmentRules.FileContentMismatchCode;
    public const string AttachmentNotAvailable = Domain.Work.AttachmentRules.NotAvailableCode;
    public const string FilterNameTaken = "FilterNameTaken";
    public const string InvalidLabel = Domain.Work.Label.InvalidLabelCode;
    public const string TooManyLabels = Domain.Work.Label.TooManyLabelsCode;

    // Jira parity pass.
    public const string ParentRequired = "ParentRequired";
    public const string InvalidParent = "InvalidParent";
    public const string NoStandardType = Domain.Projects.Project.NoStandardTypeCode;
    public const string WorkTypeInUse = Domain.Projects.Project.WorkTypeInUseCode;
    public const string ColorNotAllowed = Domain.Work.WorkItem.ColorNotAllowedCode;
    public const string SubtaskFollowsParent = "SubtaskFollowsParent";
}
