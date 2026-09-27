using System.Security.Cryptography;
using Upms.Application.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>16-character temporary passwords from a cryptographic random source (research R6).
/// Look-alike characters (0/O, 1/l/I) are left out because the password is read out or copied by hand.</summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public const int Length = 16;
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Generate(string userName)
    {
        while (true)
        {
            var password = RandomNumberGenerator.GetString(Alphabet, Length);
            if (string.IsNullOrEmpty(userName) || !password.Contains(userName, StringComparison.OrdinalIgnoreCase))
            {
                return password;
            }
        }
    }
}
