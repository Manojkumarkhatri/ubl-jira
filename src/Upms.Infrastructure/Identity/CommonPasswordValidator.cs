using Microsoft.AspNetCore.Identity;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>Refuses passwords longer than 128 characters (OWASP ASVS 2.1.2) and passwords that are easy to guess
/// (ASVS 2.1.7): the common passwords of 12 or more characters from breach data, a single repeated character, a
/// straight run such as "123456789012", and passwords containing the person's display name or e-mail name.</summary>
public sealed class CommonPasswordValidator : IPasswordValidator<User>
{
    public const string ErrorCode = "PasswordTooCommon";
    public const string TooLongCode = "PasswordTooLong";

    private static readonly Lazy<HashSet<string>> CommonPasswords = new(Load);

    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        if (password?.Length > User.PasswordMaxLength)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = TooLongCode,
                Description = $"A password can have at most {User.PasswordMaxLength} characters.",
            }));
        }

        if (string.IsNullOrEmpty(password) || !IsEasyToGuess(password, user))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = ErrorCode,
            Description = "This password is too common or too easy to guess. Choose a longer phrase that only you would use.",
        }));
    }

    public static bool IsEasyToGuess(string password, User user)
    {
        var lower = password.ToLowerInvariant();
        return CommonPasswords.Value.Contains(lower)
            || lower.Distinct().Count() == 1
            || IsStraightRun(lower)
            || ContainsPersonalWord(lower, user.DisplayName)
            || ContainsPersonalWord(lower, user.Email?.Split('@')[0]);
    }

    private static bool IsStraightRun(string password)
    {
        for (var i = 1; i < password.Length; i++)
        {
            var step = password[i] - password[i - 1];
            if (step != password[1] - password[0] || Math.Abs(step) != 1)
            {
                return false;
            }
        }

        return true;
    }

    // Parts of the name of four or more letters, such as "amina" in "Amina Khan".
    private static bool ContainsPersonalWord(string password, string? source) =>
        (source ?? "").ToLowerInvariant()
            .Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Any(word => word.Length >= 4 && password.Contains(word, StringComparison.Ordinal));

    private static HashSet<string> Load()
    {
        using var stream = typeof(CommonPasswordValidator).Assembly.GetManifestResourceStream("Upms.CommonPasswords.txt")
            ?? throw new InvalidOperationException("The common password list is missing from the build.");
        using var reader = new StreamReader(stream);
        var passwords = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                passwords.Add(line);
            }
        }

        return passwords;
    }
}
