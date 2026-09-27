using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Paged reads shared by the drawer's services: sub-tasks, comments and history.</summary>
internal sealed class WorkItemReads(IAppDbContext db, IUserDirectory users)
{
    public async Task<Page<SubtaskView>> SubtasksAsync(long parentId, IReadOnlyList<StatusInfo> statuses, PageRequest page,
        CancellationToken ct)
    {
        page = page.Normalized();
        var query = db.WorkItems.AsNoTracking().Where(w => w.ParentId == parentId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(w => new { w.Key, w.Title, w.StatusId, w.Priority, w.RowVersion })
            .ToListAsync(ct);
        var items = rows.ConvertAll(r => new SubtaskView(r.Key, r.Title, ToOption(statuses, r.StatusId), r.Priority, r.RowVersion));
        return new Page<SubtaskView>(items, total, page.Page, page.PageSize);
    }

    public async Task<Page<CommentView>> CommentsAsync(long workItemId, Guid viewerId, PageRequest page, CancellationToken ct)
    {
        page = page.Normalized();
        var query = db.Comments.AsNoTracking().Where(c => c.WorkItemId == workItemId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Select(c => c.AuthorId).Distinct().ToList(), ct);
        var items = rows.ConvertAll(c => ToView(c, viewerId, names));
        return new Page<CommentView>(items, total, page.Page, page.PageSize);
    }

    public async Task<Page<ChangeView>> HistoryAsync(long workItemId, PageRequest page, CancellationToken ct)
    {
        page = page.Normalized();
        var query = db.WorkItemChanges.AsNoTracking().Where(c => c.WorkItemId == workItemId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(c => c.OccurredAt).ThenBy(c => c.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Select(c => c.ActorId).Distinct().ToList(), ct);
        var items = rows.ConvertAll(c => IsComment(c.Field)
            // Comment text stays in the audit rows but is not shown again, so a deleted comment stays deleted.
            ? new ChangeView(c.OccurredAt, NameOf(names, c.ActorId), c.Field, null, null, c.Note)
            : new ChangeView(c.OccurredAt, NameOf(names, c.ActorId), c.Field, c.OldValue, c.NewValue, c.Note));
        return new Page<ChangeView>(items, total, page.Page, page.PageSize);
    }

    private static bool IsComment(WorkItemField field) =>
        field is WorkItemField.CommentAdded or WorkItemField.CommentEdited or WorkItemField.CommentDeleted;

    public static CommentView ToView(Domain.Work.Comment comment, Guid viewerId, IReadOnlyDictionary<Guid, UserDisplay> names) =>
        new(comment.Id, comment.AuthorId, NameOf(names, comment.AuthorId), comment.AuthorId == viewerId,
            comment.IsDeleted ? null : comment.Body, comment.CreatedAt, comment.EditedAt, comment.IsDeleted, comment.RowVersion);

    public static string NameOf(IReadOnlyDictionary<Guid, UserDisplay> names, Guid userId) =>
        names.TryGetValue(userId, out var user) ? user.DisplayName : "Unknown user";

    public static StatusOption ToOption(IReadOnlyList<StatusInfo> statuses, long statusId) =>
        statuses.FirstOrDefault(s => s.Id == statusId) is { } status
            ? new StatusOption(status.Id, status.Name, status.Category)
            : new StatusOption(statusId, "Unknown", StatusCategory.ToDo);
}
