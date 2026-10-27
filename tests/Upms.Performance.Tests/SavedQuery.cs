using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Performance.Tests;

/// <summary>The search a saved filter's query string stands for (contracts/ui-routes.md), as the search page builds it;
/// enough of it for the filters the seeder saves.</summary>
internal static class SavedQuery
{
    public static SearchQuery Parse(string query)
    {
        var values = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""), StringComparer.Ordinal);
        string[] List(string name) => values.TryGetValue(name, out var value) ? value.Split(',', StringSplitOptions.RemoveEmptyEntries) : [];

        return new SearchQuery(
            values.GetValueOrDefault("q"),
            ProjectKeys: List("project"),
            Types: [.. List("kind").Select(k => Enum.Parse<WorkItemType>(k, ignoreCase: true))],
            Categories: [.. List("category").Select(c => c switch { "todo" => StatusCategory.ToDo, "inprogress" => StatusCategory.InProgress, _ => StatusCategory.Done })],
            Priorities: [.. List("priority").Select(p => Enum.Parse<Priority>(p, ignoreCase: true))],
            AssignedToMe: List("assignee").Contains("me"),
            Unassigned: List("assignee").Contains("none"));
    }
}
