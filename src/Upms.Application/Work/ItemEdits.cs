using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The drawer's rules for an item's assignee and its epic or stage, shared by the drawer and the board's lane moves
/// (Jira parity pass FR-027; research R17). Nothing is saved here: the caller saves the whole change at once.</summary>
internal sealed class ItemEdits(IAppDbContext db, IProjectTeam team, IUserDirectory users, StageLocks locks, WatchList watchers)
{
    public const string NotAssignableMessage =
        "That person can't be assigned: only active people of the space who work on items can. The list of people has been updated.";

    /// <summary>Assigns the item, or unassigns it with null; only active Space admins and Members can be given work
    /// (<c>NotAssignable</c>, Phase 2 FR-016). A new assignee watches the item and, unless they assigned themselves, is told
    /// (Phase 4 FR-011, FR-015).</summary>
    public async Task<AppError?> AssignAsync(WorkItem item, Guid? userId, ChangeContext context, List<NotificationEvent> events,
        CancellationToken ct)
    {
        if (userId is { } id && id != item.AssigneeId && !await team.CanBeAssignedAsync(item.ProjectId, id, ct))
        {
            return AppError.Rule(ErrorCodes.NotAssignable, NotAssignableMessage);
        }

        var names = await users.GetAsync(new[] { item.AssigneeId, userId }.OfType<Guid>().Distinct().ToList(), ct);
        if (userId is { } newAssignee && newAssignee != item.AssigneeId)
        {
            await watchers.AddAsync(item.Id, [newAssignee], ct);
            events.Add(new NotificationEvent(NotificationKind.Assigned, item.ProjectId, item.Id, context.ActorId, [newAssignee]));
        }

        item.Assign(Person(item.AssigneeId, names), Person(userId, names), context);
        return null;
    }

    /// <summary>Puts a standard item into an epic or stage of its space by key, or takes it out with null
    /// (<c>InvalidContainer</c>, Phase 3 FR-015). Started work, in a column of <paramref name="category"/>, cannot join a
    /// locked stage (<c>StageLocked</c>, FR-033).</summary>
    public async Task<AppError?> PlaceInAsync(WorkItem item, string? containerKey, StatusCategory category, ChangeContext context,
        CancellationToken ct)
    {
        var target = containerKey is { } key ? await ContainerAsync(item.ProjectId, key, ct) : null;
        if (containerKey is not null && target is null)
        {
            return InvalidContainer();
        }

        if (target is not null && target.Id != item.ContainerId && await locks.CheckPlaceInAsync(item, category, target.Id, ct) is { } locked)
        {
            return locked;
        }

        var current = item.ContainerId is { } containerId ? await ContainerAsync(containerId, ct) : null;
        return item.PlaceIn(current, target, context)?.ToAppError();
    }

    public static AppError InvalidContainer() =>
        AppError.Rule(ErrorCodes.InvalidContainer, "Choose an epic or stage of this space; only stories, tasks, bugs and milestones go into one.");

    /// <summary>A live epic or stage of the space by key, or null.</summary>
    public Task<ContainerRef?> ContainerAsync(long projectId, string key, CancellationToken ct) =>
        db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == projectId && w.Key == key && (w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage))
            .Select(w => new ContainerRef(w.Id, w.ProjectId, w.Key, w.Title, w.Type))
            .SingleOrDefaultAsync(ct);

    /// <summary>The item's present container, deleted or not, to name it in history.</summary>
    public Task<ContainerRef?> ContainerAsync(long containerId, CancellationToken ct) =>
        db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.Id == containerId)
            .Select(w => new ContainerRef(w.Id, w.ProjectId, w.Key, w.Title, w.Type))
            .SingleOrDefaultAsync(ct);

    public static PersonRef? Person(Guid? userId, IReadOnlyDictionary<Guid, UserDisplay> names) =>
        userId is { } id ? new PersonRef(id, WorkItemReads.NameOf(names, id)) : null;
}
