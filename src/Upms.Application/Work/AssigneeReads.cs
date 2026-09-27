using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Work;

/// <summary>Assignees as the Work module shows them: names and "can still work" marks from the project's team, and
/// names of former members from the user directory (Phase 2 research R7).</summary>
internal sealed class AssigneeReads(IProjectTeam team, IUserDirectory users)
{
    public Task<IReadOnlyList<TeamMemberInfo>> TeamAsync(long projectId, CancellationToken ct) => team.GetMembersAsync(projectId, ct);

    public async Task<IReadOnlyDictionary<Guid, AssigneeRef>> DescribeAsync(IReadOnlyList<TeamMemberInfo> members,
        IEnumerable<Guid> assigneeIds, CancellationToken ct)
    {
        var described = new Dictionary<Guid, AssigneeRef>();
        var formerMembers = new List<Guid>();
        foreach (var id in assigneeIds.Distinct())
        {
            if (members.FirstOrDefault(m => m.UserId == id) is { } member)
            {
                described[id] = AssigneeRef.Of(id, member.DisplayName, member.CanWork);
            }
            else
            {
                formerMembers.Add(id);
            }
        }

        if (formerMembers.Count > 0)
        {
            var names = await users.GetAsync(formerMembers, ct);
            foreach (var id in formerMembers)
            {
                described[id] = AssigneeRef.Of(id, WorkItemReads.NameOf(names, id), canWork: false);
            }
        }

        return described;
    }

    /// <summary>The people who can be assigned, by name; none for a caller who cannot contribute.</summary>
    public static IReadOnlyList<AssigneeOption> Options(IReadOnlyList<TeamMemberInfo> members, Guid viewerId, bool canContribute) =>
        canContribute
            ? members.Where(m => m.CanWork).Select(m => new AssigneeOption(m.UserId, m.DisplayName, m.UserId == viewerId)).ToList()
            : [];
}
