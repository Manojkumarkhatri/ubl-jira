namespace Upms.Domain.Common;

/// <summary>A rule or validation failure reported by an entity. The application layer maps it to an
/// <c>AppError</c> (validation when <see cref="Field"/> is set, otherwise a rule violation).</summary>
public sealed record DomainError(string Code, string Message, string? Field = null)
{
    public const string ValidationCode = "Validation";

    public bool IsValidation => Field is not null;

    public static DomainError Invalid(string field, string message) => new(ValidationCode, message, field);

    public static DomainError Rule(string code, string message) => new(code, message);
}

/// <summary>A value or a <see cref="DomainError"/>.</summary>
public readonly record struct DomainResult<T>
{
    private DomainResult(T? value, DomainError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public static DomainResult<T> Ok(T value) => new(value, null);

    public static DomainResult<T> Fail(DomainError error) => new(default, error);

    public static implicit operator DomainResult<T>(T value) => Ok(value);

    public static implicit operator DomainResult<T>(DomainError error) => Fail(error);
}
