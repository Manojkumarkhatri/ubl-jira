namespace Upms.Application.Common.Results;

/// <summary>Why an application-service call failed.</summary>
/// <param name="Kind">The error category.</param>
/// <param name="Code">A stable machine-readable code, for example <see cref="ErrorCodes.DuplicateProjectKey"/>.</param>
/// <param name="Message">A message that can be shown to the user.</param>
/// <param name="FieldErrors">Validation messages per input field.</param>
/// <param name="Current">For conflicts, the latest state of the item.</param>
public sealed record AppError(
    ErrorKind Kind,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    object? Current = null)
{
    public static AppError NotFound(string what = "item") =>
        new(ErrorKind.NotFound, ErrorCodes.NotFound, $"The {what} was not found.");

    public static AppError Forbidden(string message = "You do not have permission to do that.") =>
        new(ErrorKind.Forbidden, ErrorCodes.Forbidden, message);

    public static AppError Validation(string field, string message) =>
        new(ErrorKind.Validation, ErrorCodes.Validation, message,
            new Dictionary<string, string[]> { [field] = [message] });

    public static AppError Conflict(string message, object? current = null) =>
        new(ErrorKind.Conflict, ErrorCodes.Conflict, message, Current: current);

    public static AppError Rule(string code, string message) =>
        new(ErrorKind.RuleViolation, code, message);
}
