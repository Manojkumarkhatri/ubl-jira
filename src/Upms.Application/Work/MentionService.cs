using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Work;

internal sealed class MentionService(IAppDbContext db, IProjectAccess access, IProjectTeam team) : IMentionService
{
    public async Task<Result<IReadOnlyList<PersonView>>> SuggestAsync(string workItemKey, string term, CancellationToken ct)
    {
        var projectId = await db.WorkItems.AsNoTracking()
            .Where(w => w.Key == workItemKey)
            .Select(w => (long?)w.ProjectId)
            .SingleOrDefaultAsync(ct);
        if (projectId is null)
        {
            return AppError.NotFound("task");
        }

        return await SuggestAsync(await access.RequireAsync(projectId.Value, ProjectRight.Contribute, ct), term, ct);
    }

    public async Task<Result<IReadOnlyList<PersonView>>> SuggestInSpaceAsync(string spaceKey, string term, CancellationToken ct) =>
        await SuggestAsync(await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct), term, ct);

    private async Task<Result<IReadOnlyList<PersonView>>> SuggestAsync(Result<ProjectAccessInfo> allowed, string term, CancellationToken ct)
    {
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var members = await team.FindMembersAsync(allowed.Value!.ProjectId, term, IMentionService.MaxSuggestions, ct);
        return members.Select(m => new PersonView(m.UserId, m.DisplayName, m.UserName)).ToList();
    }
}
