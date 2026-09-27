using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Common;

internal static class DomainErrorExtensions
{
    /// <summary>A field error becomes a validation error (keeping its code); anything else a rule violation.</summary>
    public static AppError ToAppError(this DomainError error) =>
        error.Field is not null
            ? new AppError(ErrorKind.Validation, error.Code, error.Message,
                new Dictionary<string, string[]> { [error.Field] = [error.Message] })
            : AppError.Rule(error.Code, error.Message);
}
