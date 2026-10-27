using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A comment on a work item (FR-030): plain text from Phases 1–3, or formatted text since Phase 4 (FR-001,
/// research R4). Only its author may edit or delete it; deleting
/// leaves a "comment deleted" placeholder and keeps the row (constitution IV).</summary>
public sealed class Comment
{
    public const int BodyMaxLength = 32_000;
    public const string NotOwnedCode = "CommentNotOwned";

    private Comment()
    {
    }

    public long Id { get; private set; }

    public long WorkItemId { get; private set; }

    public Guid AuthorId { get; private set; }

    /// <summary>Plain text, or the document's JSON when <see cref="BodyFormat"/> is <see cref="TextFormat.Document"/>.</summary>
    public string Body { get; private set; } = "";

    public TextFormat BodyFormat { get; private set; }

    /// <summary>The plain text, for search and notifications.</summary>
    public string BodyText { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when edited; the comment then shows as edited.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>A plain-text comment, as in Phases 1–3.</summary>
    public static DomainResult<Comment> Create(long workItemId, Guid authorId, string body, DateTimeOffset now)
    {
        if (Validate(body) is { } error)
        {
            return error;
        }

        var trimmed = body.Trim();
        return new Comment { WorkItemId = workItemId, AuthorId = authorId, Body = trimmed, BodyText = trimmed, CreatedAt = now };
    }

    /// <summary>A formatted comment (Phase 4 FR-001); its length was checked when it was parsed.</summary>
    public static DomainResult<Comment> Create(long workItemId, Guid authorId, FormattedText body, DateTimeOffset now)
    {
        if (body.IsEmpty)
        {
            return Empty();
        }

        return new Comment
        {
            WorkItemId = workItemId,
            AuthorId = authorId,
            Body = body.Json,
            BodyFormat = TextFormat.Document,
            BodyText = body.PlainText,
            CreatedAt = now,
        };
    }

    public DomainError? Edit(Guid actorId, string body, DateTimeOffset now)
    {
        if ((CheckEdit(actorId) ?? Validate(body)) is { } error)
        {
            return error;
        }

        Body = BodyText = body.Trim();
        BodyFormat = TextFormat.Plain;
        EditedAt = now;
        return null;
    }

    public DomainError? Edit(Guid actorId, FormattedText body, DateTimeOffset now)
    {
        if ((CheckEdit(actorId) ?? (body.IsEmpty ? Empty() : null)) is { } error)
        {
            return error;
        }

        Body = body.Json;
        BodyFormat = TextFormat.Document;
        BodyText = body.PlainText;
        EditedAt = now;
        return null;
    }

    public DomainError? Delete(Guid actorId, DateTimeOffset now)
    {
        if (actorId != AuthorId)
        {
            return DomainError.Rule(NotOwnedCode, "Only the author can delete a comment.");
        }

        IsDeleted = true;
        DeletedAt = now;
        return null;
    }

    private DomainError? CheckEdit(Guid actorId) =>
        actorId != AuthorId ? DomainError.Rule(NotOwnedCode, "Only the author can edit a comment.")
        : IsDeleted ? DomainError.Invalid("Body", "A deleted comment cannot be edited.")
        : null;

    private static DomainError Empty() => DomainError.Invalid("Body", "Write a comment first.");

    private static DomainError? Validate(string? body)
    {
        var trimmed = body?.Trim() ?? "";
        return trimmed.Length switch
        {
            0 => Empty(),
            > BodyMaxLength => DomainError.Invalid("Body", $"A comment can have at most {BodyMaxLength:N0} characters."),
            _ => null,
        };
    }
}
