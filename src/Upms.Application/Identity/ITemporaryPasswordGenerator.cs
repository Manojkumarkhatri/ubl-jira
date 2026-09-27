namespace Upms.Application.Identity;

/// <summary>Creates temporary passwords: 16 characters from a cryptographic random source (research R6).</summary>
public interface ITemporaryPasswordGenerator
{
    string Generate(string userName);
}
