namespace Upms.Application.Common.Results;

/// <summary>The category of a failed application-service call (contracts/application-services.md).</summary>
public enum ErrorKind
{
    /// <summary>The input is invalid; <see cref="AppError.FieldErrors"/> says which fields.</summary>
    Validation,

    /// <summary>The item does not exist, was deleted, or the caller may not know it exists (FR-009).</summary>
    NotFound,

    /// <summary>The caller is signed in but lacks the right.</summary>
    Forbidden,

    /// <summary>Someone else changed the item first; <see cref="AppError.Current"/> carries its latest state.</summary>
    Conflict,

    /// <summary>A business rule refused the change; <see cref="AppError.Code"/> names the rule.</summary>
    RuleViolation,
}
