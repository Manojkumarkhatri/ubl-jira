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
}
