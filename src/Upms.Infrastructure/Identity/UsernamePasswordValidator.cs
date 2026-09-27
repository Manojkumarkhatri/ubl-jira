using Microsoft.AspNetCore.Identity;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>Rejects passwords that contain the user name, ignoring case (FR-005).</summary>
public sealed class UsernamePasswordValidator : IPasswordValidator<User>
{
    public const string ErrorCode = "PasswordContainsUserName";

    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        var userName = user.UserName;
        if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(userName)
            && password.Contains(userName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = ErrorCode,
                Description = "The password must not contain your user name.",
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
