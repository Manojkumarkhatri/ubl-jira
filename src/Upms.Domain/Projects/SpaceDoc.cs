using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Projects;

/// <summary>A page of the space's Docs, as Jira's space Docs: a title and formatted text the team keeps beside its work.</summary>
public sealed class SpaceDoc
{
    public const int TitleMaxLength = 200;

    private SpaceDoc()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public string Title { get; private set; } = "";

    /// <summary>The checked editor document (<see cref="FormattedText.Json"/>), or null when empty.</summary>
    public string? Body { get; private set; }

    /// <summary>The body's text alone, for the list's preview and for search.</summary>
    public string? BodyText { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UpdatedById { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DomainResult<SpaceDoc> Create(long projectId, string title, Guid userId, DateTimeOffset now)
    {
        var doc = new SpaceDoc { ProjectId = projectId, CreatedById = userId, CreatedAt = now };
        if (doc.Edit(title, null, userId, now) is { } error)
        {
            return error;
        }

        return doc;
    }

    /// <summary>Changes the title and the body; an empty body clears it.</summary>
    public DomainError? Edit(string title, FormattedText? body, Guid userId, DateTimeOffset now)
    {
        var trimmed = (title ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > TitleMaxLength)
        {
            return DomainError.Invalid("Title", $"Enter a title of up to {TitleMaxLength} characters.");
        }

        Title = trimmed;
        Body = body is null || body.IsEmpty ? null : body.Json;
        BodyText = body is null || body.IsEmpty ? null : body.PlainText;
        UpdatedById = userId;
        UpdatedAt = now;
        return null;
    }
}
