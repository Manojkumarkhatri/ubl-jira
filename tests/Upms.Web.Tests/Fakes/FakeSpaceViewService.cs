using Upms.Application.Common.Results;
using Upms.Application.Projects;

namespace Upms.Web.Tests.Fakes;

/// <summary>No saved tabs or shortcuts: the space header shows its template's views.</summary>
public sealed class FakeSpaceViewService : ISpaceViewService
{
    public Task<Result<SpaceViews>> GetAsync(string spaceKey, CancellationToken ct) =>
        Task.FromResult<Result<SpaceViews>>(AppError.NotFound("space"));

    public Task<Result<SpaceViews>> SetViewsAsync(string spaceKey, IReadOnlyList<string> views, CancellationToken ct) =>
        Task.FromResult<Result<SpaceViews>>(AppError.NotFound("space"));

    public Task<Result<SpaceViews>> AddShortcutAsync(string spaceKey, string name, string url, CancellationToken ct) =>
        Task.FromResult<Result<SpaceViews>>(AppError.NotFound("space"));

    public Task<Result<SpaceViews>> RemoveShortcutAsync(string spaceKey, long shortcutId, CancellationToken ct) =>
        Task.FromResult<Result<SpaceViews>>(AppError.NotFound("space"));
}
