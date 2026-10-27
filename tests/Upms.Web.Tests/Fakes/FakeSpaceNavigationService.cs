using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;

namespace Upms.Web.Tests.Fakes;

/// <summary>Stars and visits in memory: WEB (Website Revamp), MOB (Mobile App) and CBU (Core Banking) can be seen.</summary>
public sealed class FakeSpaceNavigationService : ISpaceNavigationService
{
    public Dictionary<string, (string Name, ProjectTemplate Template)> Spaces { get; } = new()
    {
        ["WEB"] = ("Website Revamp", ProjectTemplate.Kanban),
        ["MOB"] = ("Mobile App", ProjectTemplate.Scrum),
        ["CBU"] = ("Core Banking", ProjectTemplate.StageGate),
    };

    public HashSet<string> Starred { get; } = [];

    public List<string> Visits { get; } = [];

    public bool IsAdministrator { get; set; }

    public int SidebarLoads { get; private set; }

    public Task<Result<SidebarView>> GetSidebarAsync(CancellationToken ct)
    {
        SidebarLoads++;
        var starred = Starred.Order(StringComparer.Ordinal).Select(Link).ToList();
        var recent = Enumerable.Reverse(Visits).Distinct().Select(Link).ToList();
        var spaces = starred.Concat(recent.Where(r => !Starred.Contains(r.Key))).Take(ISpaceNavigationService.SidebarSpaces).ToList();
        return Task.FromResult(Result<SidebarView>.Ok(new SidebarView(starred, recent.Take(ISpaceNavigationService.RecentSpaces).ToList(),
            spaces, IsAdministrator)));
    }

    public Task<Result> StarAsync(string spaceKey, CancellationToken ct)
    {
        Starred.Add(spaceKey);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> UnstarAsync(string spaceKey, CancellationToken ct)
    {
        Starred.Remove(spaceKey);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> RecordVisitAsync(string spaceKey, CancellationToken ct)
    {
        Visits.Add(spaceKey);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<IReadOnlyList<SpaceLink>>> StarredAsync(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<SpaceLink>>.Ok(Starred.Order(StringComparer.Ordinal).Select(Link).ToList()));

    private SpaceLink Link(string key) =>
        new(key, Spaces.TryGetValue(key, out var space) ? space.Name : key, Spaces.TryGetValue(key, out var s) ? s.Template : ProjectTemplate.Kanban,
            Starred.Contains(key));
}
