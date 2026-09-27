using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;

namespace Upms.Application.Projects;

internal sealed class ProjectWorkflow(IAppDbContext db) : IProjectWorkflow
{
    public async Task<BoardInfo> GetBoardInfoAsync(long projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Key, p.Name, p.BoardVersion })
            .SingleAsync(ct);
        return new BoardInfo(project.Id, project.Key, project.Name, project.BoardVersion, await StatusesAsync(projectId, ct));
    }

    public async Task<IReadOnlyList<StatusInfo>> StatusesAsync(long projectId, CancellationToken ct) =>
        await db.ProjectStatuses.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Position)
            .Select(s => new StatusInfo(s.Id, s.Name, s.Category, s.Position, s.WipLimit))
            .ToListAsync(ct);

    public async Task<StatusInfo> FirstToDoStatusAsync(long projectId, CancellationToken ct) =>
        (await StatusesAsync(projectId, ct)).First(s => s.Category == StatusCategory.ToDo);
}
