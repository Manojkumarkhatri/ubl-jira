using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Labels on work items (Phase 4 FR-042–FR-044, FR-048, research R18): one shared label per name in any case,
/// suggested only from projects the caller can see, and every change recorded in the item's history.</summary>
internal sealed class LabelService(IAppDbContext db, IProjectAccess access, TimeProvider time) : ILabelService
{
    public const int SuggestionLimit = 10;

    public async Task<Result<IReadOnlyList<string>>> SuggestAsync(string projectKey, string term, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        // Only labels on live items in projects the caller can see: a label used only elsewhere stays private (FR-044).
        var prefix = Label.Normalize(term);
        var items = db.WorkItems.AsNoTracking();
        if (await access.VisibleProjectIdsAsync(ct) is { } visible)
        {
            var ids = visible.ToList();
            items = items.Where(w => ids.Contains(w.ProjectId));
        }

        var names = await db.Labels.AsNoTracking()
            .Where(l => l.NormalizedName.StartsWith(prefix)
                && db.WorkItemLabels.Any(x => x.LabelId == l.Id && items.Any(w => w.Id == x.WorkItemId)))
            .OrderBy(l => l.NormalizedName)
            .Select(l => l.Name)
            .Take(SuggestionLimit)
            .ToListAsync(ct);
        return names;
    }

    public async Task<Result<IReadOnlyList<string>>> AddAsync(string workItemKey, string label, CancellationToken ct)
    {
        var (item, userId, error) = await LoadAsync(workItemKey, ct);
        if (error is not null)
        {
            return error;
        }

        if (Label.Check(label) is { } invalid)
        {
            return invalid.ToAppError();
        }

        var normalized = Label.Normalize(label);
        var onItem = await db.WorkItemLabels.AsNoTracking()
            .Where(x => x.WorkItemId == item!.Id)
            .Join(db.Labels, x => x.LabelId, l => l.Id, (x, l) => l.NormalizedName)
            .ToListAsync(ct);
        if (onItem.Contains(normalized))
        {
            return Result<IReadOnlyList<string>>.Ok(await LabelReads.ForItemAsync(db, item!.Id, ct));
        }

        if (Label.CheckRoom(onItem.Count) is { } full)
        {
            return full.ToAppError();
        }

        var shared = await SharedAsync(db, label, ct);
        var now = time.GetUtcNow();
        db.WorkItemLabels.Add(WorkItemLabel.Add(item!.Id, shared.Id, userId, now));
        item.RecordActivity(WorkItemField.LabelAdded, null, shared.Name, ChangeContext.New(userId, now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone added the same label to the item at the same moment; it is there either way.
        }

        return Result<IReadOnlyList<string>>.Ok(await LabelReads.ForItemAsync(db, item.Id, ct));
    }

    /// <summary>The shared label with this name, created if no label has it yet. The first spelling wins (FR-043): the label
    /// is created only if no label has this name in any case, in one statement that holds its range lock, so two people
    /// adding it at once share one row. The label must have passed <see cref="Label.Check"/>.</summary>
    internal static async Task<Label> SharedAsync(IAppDbContext db, string label, CancellationToken ct)
    {
        var normalized = Label.Normalize(label);
        var name = Label.Create(label).Value!.Name;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO [Labels] ([Name], [NormalizedName])
            SELECT {name}, {normalized}
            WHERE NOT EXISTS (SELECT 1 FROM [Labels] WITH (UPDLOCK, HOLDLOCK) WHERE [NormalizedName] = {normalized})
            """, ct);
        return await db.Labels.AsNoTracking().SingleAsync(l => l.NormalizedName == normalized, ct);
    }

    public async Task<Result<IReadOnlyList<string>>> RemoveAsync(string workItemKey, string label, CancellationToken ct)
    {
        var (item, userId, error) = await LoadAsync(workItemKey, ct);
        if (error is not null)
        {
            return error;
        }

        var normalized = Label.Normalize(label);
        var link = await db.WorkItemLabels
            .Where(x => x.WorkItemId == item!.Id)
            .Join(db.Labels, x => x.LabelId, l => l.Id, (x, l) => new { Link = x, l.Name, l.NormalizedName })
            .SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
        if (link is not null)
        {
            db.WorkItemLabels.Remove(link.Link);
            item!.RecordActivity(WorkItemField.LabelRemoved, link.Name, null, ChangeContext.New(userId, time.GetUtcNow()));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone removed it at the same moment; it is gone either way.
            }
        }

        return Result<IReadOnlyList<string>>.Ok(await LabelReads.ForItemAsync(db, item!.Id, ct));
    }

    /// <summary>The live item, tracked, if the caller may contribute to its project; people outside it get <c>NotFound</c>.</summary>
    private async Task<(WorkItem? Item, Guid UserId, AppError? Error)> LoadAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return (null, Guid.Empty, AppError.NotFound("task"));
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        return allowed.IsSuccess ? (item, allowed.Value!.UserId, null) : (null, Guid.Empty, allowed.Error);
    }
}
