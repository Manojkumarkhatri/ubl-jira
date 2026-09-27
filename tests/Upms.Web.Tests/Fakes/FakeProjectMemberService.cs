using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Projects;

namespace Upms.Web.Tests.Fakes;

/// <summary>An in-memory team for WEB: owen (Project Admin, the viewer), amina (Member), bilal (Viewer) and gone
/// (deactivated Member); records calls.</summary>
public sealed class FakeProjectMemberService(Guid me) : IProjectMemberService
{
    private static readonly DateTimeOffset Added = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    public static readonly Guid Amina = Guid.NewGuid();
    public static readonly Guid Bilal = Guid.NewGuid();
    public static readonly Guid Gone = Guid.NewGuid();
    public static readonly Guid Carla = Guid.NewGuid();

    public List<MemberView> Members { get; } =
    [
        new(me, "Owen Tester", "owen", ProjectRole.ProjectAdmin, true, true, Added),
        new(Amina, "Amina Khan", "amina", ProjectRole.Member, true, false, Added),
        new(Bilal, "Bilal Ahmed", "bilal", ProjectRole.Viewer, true, false, Added),
        new(Gone, "Gone Away", "gone", ProjectRole.Member, false, false, Added),
    ];

    public List<PersonOption> People { get; } =
    [
        new(Carla, "Carla Diaz", "carla"),
        new(Guid.NewGuid(), "Carlos Moreno", "carlos"),
    ];

    public bool CanManage { get; set; } = true;

    public int Version { get; set; } = 4;

    public List<string> Calls { get; } = [];

    public List<string> Searches { get; } = [];

    /// <summary>Returned instead of applying the next change.</summary>
    public Func<Result<TeamView>>? NextResult { get; set; }

    public TeamView View() => new("WEB", "Website Revamp", Version, CanManage, Members.ToList());

    public Task<Result<TeamView>> GetTeamAsync(string projectKey, CancellationToken ct) =>
        Task.FromResult(Result<TeamView>.Ok(View()));

    public Task<Result<IReadOnlyList<PersonOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct)
    {
        Searches.Add(term);
        IReadOnlyList<PersonOption> found = People
            .Where(p => p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) || p.UserName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<PersonOption>>.Ok(found));
    }

    public Task<Result<TeamView>> AddAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion, CancellationToken ct) =>
        Apply($"add {NameOf(userId)} {role} v{expectedMembersVersion}", () =>
        {
            var person = People.Single(p => p.UserId == userId);
            Members.Add(new MemberView(userId, person.DisplayName, person.UserName, role, true, false, Added));
            People.Remove(person);
        });

    public Task<Result<TeamView>> ChangeRoleAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion,
        CancellationToken ct) =>
        Apply($"role {NameOf(userId)} {role} v{expectedMembersVersion}", () =>
        {
            var index = Members.FindIndex(m => m.UserId == userId);
            Members[index] = Members[index] with { Role = role };
        });

    public Task<Result<TeamView>> RemoveAsync(string projectKey, Guid userId, int expectedMembersVersion, CancellationToken ct) =>
        Apply($"remove {NameOf(userId)} v{expectedMembersVersion}", () => Members.RemoveAll(m => m.UserId == userId));

    private string NameOf(Guid userId) =>
        Members.FirstOrDefault(m => m.UserId == userId)?.UserName ?? People.FirstOrDefault(p => p.UserId == userId)?.UserName ?? "?";

    private Task<Result<TeamView>> Apply(string call, Action change)
    {
        Calls.Add(call);
        if (NextResult is { } next)
        {
            NextResult = null;
            return Task.FromResult(next());
        }

        change();
        Version++;
        return Task.FromResult(Result<TeamView>.Ok(View()));
    }
}
