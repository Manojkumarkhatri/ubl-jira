using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Each series is one grouped query by UTC day over the space's live items; lead and cycle times read the resolved
/// items' dates and their status history.</summary>
internal sealed class SpaceReportService(IAppDbContext db, IProjectAccess access, IProjectWorkflow workflow, TimeProvider time)
    : ISpaceReportService
{
    /// <summary>Enough resolved items for any space's averages, while keeping the read bounded.</summary>
    private const int MaxResolvedRead = 20_000;

    public async Task<Result<CreatedVsResolved>> CreatedVsResolvedAsync(string spaceKey, int days, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var projectId = allowed.Value!.ProjectId;
        days = Math.Clamp(days, 7, 365);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var first = today.AddDays(1 - days);
        var since = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var items = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId); // live items only

        var created = await items.Where(w => w.CreatedAt >= since)
            .GroupBy(w => new { w.CreatedAt.Year, w.CreatedAt.Month, w.CreatedAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync(ct);
        var resolved = await items.Where(w => w.ResolvedAt != null && w.ResolvedAt >= since)
            .Select(w => new { w.Id, w.CreatedAt, ResolvedAt = w.ResolvedAt!.Value })
            .Take(MaxResolvedRead)
            .ToListAsync(ct);
        var open = await items.CountAsync(w => w.CreatedAt < since && (w.ResolvedAt == null || w.ResolvedAt >= since), ct);

        // When each resolved item first moved into an "in progress" status, by the status names history records.
        var inProgress = (await workflow.GetBoardInfoAsync(projectId, ct)).Statuses
            .Where(s => s.Category == StatusCategory.InProgress).Select(s => s.Name).ToList();
        var started = await (
                from change in db.WorkItemChanges.AsNoTracking()
                join item in items on change.WorkItemId equals item.Id
                where item.ResolvedAt != null && item.ResolvedAt >= since && change.Field == WorkItemField.Status
                    && change.NewValue != null && inProgress.Contains(change.NewValue)
                group change by change.WorkItemId into g
                select new { WorkItemId = g.Key, At = g.Min(c => c.OccurredAt) })
            .ToDictionaryAsync(s => s.WorkItemId, s => s.At, ct);

        var createdOn = created.ToDictionary(c => new DateOnly(c.Year, c.Month, c.Day), c => c.Count);
        var resolvedOn = resolved.ToLookup(r => DateOnly.FromDateTime(r.ResolvedAt.UtcDateTime));
        var rows = new List<DayCounts>(days);
        for (var day = first; day <= today; day = day.AddDays(1))
        {
            var c = createdOn.GetValueOrDefault(day);
            var done = resolvedOn[day].ToList();
            var cycles = done.Where(r => started.ContainsKey(r.Id) && started[r.Id] <= r.ResolvedAt)
                .Select(r => (r.ResolvedAt - started[r.Id]).TotalDays).ToList();
            open = Math.Max(0, open + c - done.Count);
            rows.Add(new DayCounts(day, c, done.Count, open,
                done.Count == 0 ? null : done.Average(r => (r.ResolvedAt - r.CreatedAt).TotalDays),
                cycles.Count == 0 ? null : cycles.Average()));
        }

        return new CreatedVsResolved(rows, rows.Sum(d => d.Created), rows.Sum(d => d.Resolved));
    }
}
