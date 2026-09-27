using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Board columns (FR-034 to FR-041). Rules live in <see cref="Project"/>; this service loads the
/// board, checks the caller's right and the board version, and saves each change in one transaction.</summary>
internal sealed class BoardColumnService(
    IAppDbContext db,
    IProjectAccess access,
    IWorkItemCounts counts,
    IWorkItemStatusMover mover,
    TimeProvider time) : IBoardColumnService
{
    public const string ColumnDeletedNote = "column deleted";

    public async Task<Result<BoardColumnsView>> GetAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        return allowed.IsSuccess ? await ViewAsync(allowed.Value!.ProjectId, ct) : allowed.Error!;
    }

    public Task<Result<BoardColumnsView>> AddAsync(string projectKey, string name, StatusCategory category, int position,
        int expectedBoardVersion, CancellationToken ct) =>
        ChangeAsync(projectKey, expectedBoardVersion, project =>
            Task.FromResult(project.AddColumn(name, category, position, time.GetUtcNow()).Error?.ToAppError()), ct);

    public Task<Result<BoardColumnsView>> RenameAsync(string projectKey, long columnId, string name, int expectedBoardVersion,
        CancellationToken ct) =>
        ChangeColumnAsync(projectKey, columnId, expectedBoardVersion,
            (project, column) => Task.FromResult(project.RenameColumn(column, name, time.GetUtcNow())), ct);

    public Task<Result<BoardColumnsView>> MoveAsync(string projectKey, long columnId, int newPosition, int expectedBoardVersion,
        CancellationToken ct) =>
        ChangeColumnAsync(projectKey, columnId, expectedBoardVersion,
            (project, column) => Task.FromResult(project.MoveColumn(column, newPosition, time.GetUtcNow())), ct);

    public Task<Result<BoardColumnsView>> SetWipLimitAsync(string projectKey, long columnId, int? limit, int expectedBoardVersion,
        CancellationToken ct) =>
        ChangeColumnAsync(projectKey, columnId, expectedBoardVersion,
            (project, column) => Task.FromResult(project.SetWipLimit(column, limit, time.GetUtcNow())), ct);

    public Task<Result<BoardColumnsView>> ChangeCategoryAsync(string projectKey, long columnId, StatusCategory category,
        int expectedBoardVersion, CancellationToken ct) =>
        ChangeColumnAsync(projectKey, columnId, expectedBoardVersion, async (project, column) =>
        {
            var items = (await mover.CountInStatusesAsync([column.Id], ct)).GetValueOrDefault(column.Id);
            return project.ChangeColumnCategory(column, category, items, time.GetUtcNow());
        }, ct);

    public Task<Result<BoardColumnsView>> DeleteAsync(string projectKey, long columnId, long? destinationColumnId,
        int expectedBoardVersion, CancellationToken ct) =>
        ChangeAsync(projectKey, expectedBoardVersion, async project =>
        {
            if (Find(project, columnId) is not { } column)
            {
                return AppError.NotFound("column");
            }

            ProjectStatus? destination = null;
            if (destinationColumnId is { } destinationId && (destination = Find(project, destinationId)) is null)
            {
                return AppError.Validation("DestinationColumnId", "Choose a column on this board for its work items.");
            }

            // Work items move first, recorded in their history (FR-037); the same save removes the column.
            var items = (await mover.CountInStatusesAsync([column.Id], ct)).GetValueOrDefault(column.Id);
            if (project.RemoveColumn(column, destination, items, time.GetUtcNow()) is { } error)
            {
                return error.ToAppError();
            }

            if (items > 0)
            {
                await mover.MoveAllAsync(column.Id, destination!.Id, ColumnDeletedNote, ct);
            }

            return null;
        }, ct);

    private Task<Result<BoardColumnsView>> ChangeColumnAsync(string projectKey, long columnId, int expectedBoardVersion,
        Func<Project, ProjectStatus, Task<DomainError?>> change, CancellationToken ct) =>
        ChangeAsync(projectKey, expectedBoardVersion, async project =>
            Find(project, columnId) is { } column
                ? (await change(project, column))?.ToAppError()
                : AppError.NotFound("column"), ct);

    /// <summary>Checks the right and the version, applies the change and saves it in one transaction.</summary>
    private async Task<Result<BoardColumnsView>> ChangeAsync(string projectKey, int expectedBoardVersion,
        Func<Project, Task<AppError?>> change, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var projectId = allowed.Value!.ProjectId;
        var project = await db.Projects.Include(p => p.Statuses).SingleAsync(p => p.Id == projectId, ct);
        if (project.BoardVersion != expectedBoardVersion)
        {
            return await ConflictAsync(projectId, ct);
        }

        if (await change(project) is { } error)
        {
            return error;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another change to this board (a column change, or a card moved into a column being deleted)
            // was saved first: the version check, a unique column name or a status reference refused this one.
            return await ConflictAsync(projectId, ct);
        }

        return await ViewAsync(projectId, ct);
    }

    private static ProjectStatus? Find(Project project, long columnId) => project.Statuses.FirstOrDefault(s => s.Id == columnId);

    private async Task<AppError> ConflictAsync(long projectId, CancellationToken ct) =>
        AppError.Conflict("The columns were changed by someone else. The latest columns are shown; check them and try again.",
            await ViewAsync(projectId, ct));

    private async Task<BoardColumnsView> ViewAsync(long projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Key, p.Name, p.BoardVersion })
            .SingleAsync(ct);
        var statuses = await db.ProjectStatuses.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);
        var ids = statuses.ConvertAll(s => s.Id);
        var visible = await counts.CountByStatusAsync(ids, ct);
        var all = await mover.CountInStatusesAsync(ids, ct);
        return new BoardColumnsView(project.Key, project.Name, project.BoardVersion, statuses.ConvertAll(s =>
            new BoardColumnView(s.Id, s.Name, s.Category, s.Position, s.WipLimit, visible.GetValueOrDefault(s.Id),
                all.GetValueOrDefault(s.Id) == 0)));
    }
}
