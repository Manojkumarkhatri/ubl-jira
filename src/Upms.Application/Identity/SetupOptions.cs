namespace Upms.Application.Identity;

/// <summary>First-run setup configuration (research R8). The token comes from configuration
/// (<c>Setup:Token</c>), never from committed files.</summary>
public sealed class SetupOptions
{
    public const string SectionName = "Setup";

    public string? Token { get; set; }
}
