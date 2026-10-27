using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Formatted text from the editor, as the Work services keep it (Phase 4 research R3, R6): checked against the
/// allowlist, with mentions kept only for the project's active members and images only for the item's own attachments.</summary>
internal sealed class TextChecks(IAppDbContext db, IProjectTeam team)
{
    /// <param name="field"><c>Description</c> or <c>Body</c>, for error messages.</param>
    public async Task<DomainResult<FormattedText>> ParseAsync(string document, string field, long projectId, long workItemId,
        CancellationToken ct)
    {
        var parsed = FormattedText.Parse(document, field);
        if (!parsed.IsSuccess)
        {
            return parsed;
        }

        var text = parsed.Value!;
        var members = text.MentionIds.Count == 0
            ? new HashSet<Guid>()
            : (await team.GetMembersAsync(projectId, ct)).Where(m => m.IsActive).Select(m => m.UserId).ToHashSet();
        var images = text.ImageAttachmentIds.Count == 0 ? new HashSet<long>() : await ImageIdsAsync(workItemId, ct);
        return text.Restrict(members, images);
    }

    /// <summary>The people mentioned in stored text: none for plain text.</summary>
    public static IReadOnlyList<Guid> MentionsIn(TextFormat format, string? content) =>
        format == TextFormat.Document && content is not null && FormattedText.Parse(content, "Body").Value is { } text
            ? text.MentionIds
            : [];

    /// <summary>The item's images that its text may show: listed and not blocked (Phase 4 FR-023).</summary>
    private async Task<HashSet<long>> ImageIdsAsync(long workItemId, CancellationToken ct) =>
        (await db.Attachments.AsNoTracking()
            .Where(a => a.WorkItemId == workItemId && a.RemovedAt == null && a.PurgedAt == null && a.CheckStatus != AttachmentCheckStatus.Blocked)
            .Select(a => new { a.Id, a.Extension })
            .ToListAsync(ct))
        .Where(a => AttachmentRules.IsImage(a.Extension))
        .Select(a => a.Id)
        .ToHashSet();
}
