using Upms.Domain.Common;

namespace Upms.Domain.Projects;

/// <summary>A link pinned to a space's navigation, as Jira's "Shortcuts": a name and a web address the team uses often.</summary>
public sealed class SpaceShortcut
{
    public const int NameMaxLength = 60;
    public const int UrlMaxLength = 500;

    private SpaceShortcut()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public string Name { get; private set; } = "";

    public string Url { get; private set; } = "";

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>A shortcut to an http or https address only, so a link can never run script.</summary>
    public static DomainResult<SpaceShortcut> Create(long projectId, string name, string url, Guid createdById, DateTimeOffset now)
    {
        var trimmedName = (name ?? "").Trim();
        if (trimmedName.Length == 0 || trimmedName.Length > NameMaxLength)
        {
            return DomainError.Invalid("Name", $"Enter a name of up to {NameMaxLength} characters.");
        }

        var trimmedUrl = (url ?? "").Trim();
        if (trimmedUrl.Length > UrlMaxLength || !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
        {
            return DomainError.Invalid("Url", "Enter a web address that starts with https:// or http://.");
        }

        return new SpaceShortcut { ProjectId = projectId, Name = trimmedName, Url = address.ToString(), CreatedById = createdById, CreatedAt = now };
    }
}
