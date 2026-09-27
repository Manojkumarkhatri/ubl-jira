namespace Upms.Application.Common.Results;

/// <summary>The outcome of an operation that returns no value.</summary>
public readonly record struct Result
{
    private Result(AppError? error, IReadOnlyList<string>? warnings)
    {
        Error = error;
        Warnings = warnings ?? [];
    }

    public AppError? Error { get; }

    /// <summary>Messages for a successful call that the user should still see (for example FR-029).</summary>
    public IReadOnlyList<string> Warnings { get; }

    public bool IsSuccess => Error is null;

    public static Result Ok(IReadOnlyList<string>? warnings = null) => new(null, warnings);

    public static Result Fail(AppError error) => new(error, null);

    public static implicit operator Result(AppError error) => Fail(error);
}

/// <summary>The outcome of an operation that returns a value on success.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, AppError? error, IReadOnlyList<string>? warnings)
    {
        Value = value;
        Error = error;
        Warnings = warnings ?? [];
    }

    /// <summary>The value; only meaningful when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    public AppError? Error { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value, IReadOnlyList<string>? warnings = null) => new(value, null, warnings);

    public static Result<T> Fail(AppError error) => new(default, error, null);

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(AppError error) => Fail(error);

    /// <summary>The value of a successful result; throws when the call failed.</summary>
    public T ValueOrThrow() =>
        IsSuccess ? Value! : throw new InvalidOperationException($"{Error!.Kind}: {Error.Message}");
}
