using Microsoft.AspNetCore.Identity;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>User name length and display name rules (data-model.md, "User"); the built-in validator
/// checks allowed characters and uniqueness.</summary>
public sealed class UserRulesValidator : IUserValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        var errors = new List<IdentityError>();
        var userName = user.UserName ?? "";
        if (userName.Length is < User.UserNameMinLength or > User.UserNameMaxLength)
        {
            errors.Add(new IdentityError
            {
                Code = "InvalidUserNameLength",
                Description = $"The user name must be {User.UserNameMinLength}–{User.UserNameMaxLength} characters.",
            });
        }

        var displayName = user.DisplayName?.Trim() ?? "";
        if (displayName.Length is 0 or > User.DisplayNameMaxLength)
        {
            errors.Add(new IdentityError
            {
                Code = "InvalidDisplayName",
                Description = $"The display name must be 1–{User.DisplayNameMaxLength} characters.",
            });
        }

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }
}
