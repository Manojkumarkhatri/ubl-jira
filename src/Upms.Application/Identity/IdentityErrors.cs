using Microsoft.AspNetCore.Identity;
using Upms.Application.Common.Results;

namespace Upms.Application.Identity;

/// <summary>Maps ASP.NET Core Identity failures to application errors with field names.</summary>
internal static class IdentityErrors
{
    public static AppError ToAppError(IdentityResult result, string passwordField = "Password")
    {
        var errors = result.Errors.ToList();
        if (errors.Exists(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return AppError.Rule(ErrorCodes.DuplicateUserName, "That user name is already taken.");
        }

        if (errors.Exists(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AppError.Rule(ErrorCodes.DuplicateEmail, "That email address is already used by another account.");
        }

        if (errors.Exists(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return AppError.Conflict("Someone else changed this account at the same time. Try again.");
        }

        var fieldErrors = errors
            .GroupBy(e => FieldFor(e.Code, passwordField))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return new AppError(ErrorKind.Validation, ErrorCodes.Validation,
            string.Join(" ", errors.Select(e => e.Description)), fieldErrors);
    }

    private static string FieldFor(string code, string passwordField) => code switch
    {
        nameof(IdentityErrorDescriber.PasswordMismatch) => "CurrentPassword",
        _ when code.StartsWith("Password", StringComparison.Ordinal) => passwordField,
        nameof(IdentityErrorDescriber.InvalidUserName) or "InvalidUserNameLength" => "UserName",
        nameof(IdentityErrorDescriber.InvalidEmail) => "Email",
        "InvalidDisplayName" => "DisplayName",
        _ => "",
    };
}
