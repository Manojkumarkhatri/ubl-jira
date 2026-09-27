using Microsoft.EntityFrameworkCore;
using Upms.Application.Projects.Contracts;

namespace Upms.Infrastructure.Persistence;

/// <summary>Increments <c>Projects.NextItemNumber</c> in one statement inside the caller's transaction:
/// concurrent creations queue on the project row, and a rollback also rolls back the number (R13).</summary>
internal sealed class WorkItemNumberAllocator(AppDbContext db) : IWorkItemNumberAllocator
{
    public async Task<int> NextAsync(long projectId, CancellationToken ct)
    {
        var numbers = await db.Database
            .SqlQuery<int>($"UPDATE [dbo].[Projects] SET [NextItemNumber] = [NextItemNumber] + 1 OUTPUT deleted.[NextItemNumber] AS [Value] WHERE [Id] = {projectId}")
            .ToListAsync(ct);
        return numbers.Count == 1
            ? numbers[0]
            : throw new InvalidOperationException($"Project {projectId} does not exist.");
    }
}
