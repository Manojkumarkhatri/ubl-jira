using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A plain-text comment on a work item (FR-030). Only its author may edit or delete it; deleting
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

    public string Body { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when edited; the comment then shows as edited.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static DomainResult<Comment> Create(long workItemId, Guid authorId, string body, DateTimeOffset now)
    {
        if (Validate(body) is { } error)
        {
            return error;
        }

        return new Comment { WorkItemId = workItemId, AuthorId = authorId, Body = body.Trim(), CreatedAt = now };
    }

    public DomainError? Edit(Guid actorId, string body, DateTimeOffset now)
    {
        if (actorId != AuthorId)
        {
            return DomainError.Rule(NotOwnedCode, "Only the author can edit a comment.");
        }

        if (IsDeleted)
        {
            return DomainError.Invalid("Body", "A deleted comment cannot be edited.");
        }

        if (Validate(body) is { } error)
        {
            return error;
        }

        Body = body.Trim();
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

    private static DomainError? Validate(string? body)
    {
        var trimmed = body?.Trim() ?? "";
        return trimmed.Length switch
        {
            0 => DomainError.Invalid("Body", "Write a comment first."),
            > BodyMaxLength => DomainError.Invalid("Body", $"A comment can have at most {BodyMaxLength:N0} characters."),
            _ => null,
        };
    }
}
