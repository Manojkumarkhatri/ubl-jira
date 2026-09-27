using Upms.Application.Common.Results;

namespace Upms.Web.Components.Shared;

public static class AppErrorText
{
    /// <summary>The messages for one field when the error names it, otherwise the error's message.</summary>
    public static string MessageFor(this AppError error, string field) =>
        error.FieldErrors is { } errors && errors.TryGetValue(field, out var messages)
            ? string.Join(" ", messages)
            : error.Message;
}
