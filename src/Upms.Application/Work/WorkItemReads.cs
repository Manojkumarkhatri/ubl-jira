using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Paged reads shared by the drawer's services: sub-tasks, comments and history.</summary>
internal sealed class WorkItemReads(IAppDbContext db, IUserDirectory users, AssigneeReads assignees)
{
    /// <param name="team">The project's members, for the sub-tasks' assignees.</param>
    public async Task<Page<SubtaskView>> SubtasksAsync(long parentId, IReadOnlyList<StatusInfo> statuses,
        IReadOnlyList<TeamMemberInfo> team, PageRequest page, CancellationToken ct)
    {
        page = page.Normalized();
        var query = db.WorkItems.AsNoTracking().Where(w => w.ParentId == parentId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(w => new { w.Key, w.Title, w.StatusId, w.Priority, w.RowVersion, w.AssigneeId, w.DueDate })
            .ToListAsync(ct);
        var people = await assignees.DescribeAsync(team, rows.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        var items = rows.ConvertAll(r => new SubtaskView(r.Key, r.Title, ToOption(statuses, r.StatusId), r.Priority, r.RowVersion,
            r.AssigneeId is { } assigneeId ? people[assigneeId] : null, r.DueDate));
        return new Page<SubtaskView>(items, total, page.Page, page.PageSize);
    }

    /// <summary>An epic's or stage's items, by key, and its progress (Phase 3 FR-016, FR-017): its standard items other than
    /// milestones that are done (resolved), out of all of them; 100% once the container itself is done.</summary>
    public async Task<ContainerSummary> ContainedAsync(long containerId, bool containerDone, IReadOnlyList<StatusInfo> statuses,
        IReadOnlyList<TeamMemberInfo> team, PageRequest page, CancellationToken ct)
    {
        page = page.Normalized();
        var query = db.WorkItems.AsNoTracking().Where(w => w.ContainerId == containerId);
        var counted = query.Where(w => w.Type != WorkItemType.Milestone);
        var progress = new Progress(await counted.CountAsync(w => w.ResolvedAt != null, ct), await counted.CountAsync(ct), containerDone);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(w => w.Number)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(w => new { w.Key, w.Type, w.Title, w.StatusId, w.AssigneeId, w.DueDate, w.RowVersion })
            .ToListAsync(ct);
        var people = await assignees.DescribeAsync(team, rows.Where(r => r.AssigneeId != null).Select(r => r.AssigneeId!.Value), ct);
        var items = rows.ConvertAll(r => new ContainedItemView(r.Key, r.Type, r.Title, ToOption(statuses, r.StatusId),
            r.AssigneeId is { } assigneeId ? people[assigneeId] : null, r.DueDate, r.RowVersion));
        return new ContainerSummary(progress, new Page<ContainedItemView>(items, total, page.Page, page.PageSize));
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
        var bodies = await WithCurrentNamesAsync(items.ConvertAll(c => c.Body), ct);
        return new Page<CommentView>(items.Select((c, i) => c with { Body = bodies[i] }).ToList(), total, page.Page, page.PageSize);
    }

    /// <summary>The texts with each mention showing the person's current display name, or the name stored with it when the
    /// person is unknown (Phase 4 FR-010); all the people are read at once.</summary>
    public async Task<IReadOnlyList<RichText?>> WithCurrentNamesAsync(IReadOnlyList<RichText?> texts, CancellationToken ct)
    {
        var documents = texts.Select(t => t is { Format: TextFormat.Document } ? FormattedText.Parse(t.Content, "Body").Value : null).ToList();
        var people = documents.Where(d => d is not null).SelectMany(d => d!.MentionIds).Distinct().ToList();
        if (people.Count == 0)
        {
            return texts;
        }

        var names = (await users.GetAsync(people, ct)).ToDictionary(p => p.Key, p => p.Value.DisplayName);
        return texts.Select((text, i) => documents[i]?.WithMentionNames(names) is { } shown
            ? new RichText(TextFormat.Document, shown.Json, shown.PlainText)
            : text).ToList();
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
            comment.IsDeleted ? null : RichText.Of(comment.BodyFormat, comment.Body, comment.BodyText), comment.CreatedAt,
            comment.EditedAt, comment.IsDeleted, comment.RowVersion);

    public static string NameOf(IReadOnlyDictionary<Guid, UserDisplay> names, Guid userId) =>
        names.TryGetValue(userId, out var user) ? user.DisplayName : "Unknown user";

    public static StatusOption ToOption(IReadOnlyList<StatusInfo> statuses, long statusId) =>
        statuses.FirstOrDefault(s => s.Id == statusId) is { } status
            ? new StatusOption(status.Id, status.Name, status.Category)
            : new StatusOption(statusId, "Unknown", StatusCategory.ToDo);
}
