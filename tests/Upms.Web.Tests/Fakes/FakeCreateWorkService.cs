using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>The Create form's lists, configurable per test; <see cref="Hold"/> keeps option answers back until released, as a
/// slow server would.</summary>
public sealed class FakeCreateWorkService : ICreateWorkService
{
    public static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    public static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);
    public static readonly StatusOption Done = new(3, "Done", StatusCategory.Done);

    private readonly Dictionary<string, TaskCompletionSource> _held = [];

    public List<CreateSpaceOption> Spaces { get; } =
    [
        new("MOB", "Mobile App", ProjectTemplate.Scrum,
            [WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask]),
        new("WEB", "Website Revamp", ProjectTemplate.Kanban,
            [WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Subtask]),
    ];

    public string? DefaultKey { get; set; } = "WEB";

    public List<string?> SpaceRequests { get; } = [];

    public List<string> OptionRequests { get; } = [];

    public List<ParentOption> Parents { get; } =
    [
        new("WEB-1", "Card refunds", WorkItemType.Epic),
        new("WEB-2", "Refund to card", WorkItemType.Story),
    ];

    public static readonly Guid Amina = Guid.Parse("0000000a-0000-0000-0000-000000000000");
    public static readonly Guid Omar = Guid.Parse("0000000c-0000-0000-0000-000000000000");

    /// <summary>Holds the options of this space until <see cref="Release"/>.</summary>
    public void Hold(string spaceKey) => _held[spaceKey] = new TaskCompletionSource();

    public void Release(string spaceKey) => _held[spaceKey].SetResult();

    public Task<Result<CreateSpaces>> SpacesAsync(string? currentSpaceKey, CancellationToken ct)
    {
        SpaceRequests.Add(currentSpaceKey);
        var current = Spaces.Any(s => s.Key == currentSpaceKey) ? currentSpaceKey : DefaultKey;
        return Task.FromResult(Result<CreateSpaces>.Ok(new CreateSpaces(Spaces, Spaces.Count == 0 ? null : current)));
    }

    public async Task<Result<CreateOptions>> OptionsAsync(string spaceKey, CancellationToken ct)
    {
        OptionRequests.Add(spaceKey);
        if (_held.TryGetValue(spaceKey, out var held))
        {
            await held.Task;
        }

        var space = Spaces.Single(s => s.Key == spaceKey);
        return new CreateOptions(space.Key, space.Template, space.Types, [ToDo, InProgress, Done],
            [new AssigneeOption(Amina, "Amina Khan", true), new AssigneeOption(Omar, "Omar Ali", false)],
            [new ContainerOption($"{space.Key}-1", "Card refunds", WorkItemType.Epic, false)],
            space.Template == ProjectTemplate.Scrum ? [new SprintOption(7, "MOB Sprint 3")] : []);
    }

    /// <summary>The space fields the form shows; none unless a test sets them.</summary>
    public IReadOnlyList<CustomFieldView> Fields { get; set; } = [];

    public Task<Result<IReadOnlyList<CustomFieldView>>> FieldsAsync(string spaceKey, WorkItemType type, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<CustomFieldView>>.Ok(Fields));

    public Task<Result<IReadOnlyList<ParentOption>>> FindParentsAsync(string spaceKey, WorkItemType type, string text, CancellationToken ct)
    {
        IReadOnlyList<ParentOption> found = Parents
            .Where(p => type == WorkItemType.Subtask ? p.Type != WorkItemType.Epic : p.Type == WorkItemType.Epic)
            .Where(p => p.Key.Contains(text, StringComparison.OrdinalIgnoreCase) || p.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<ParentOption>>.Ok(found));
    }
}
