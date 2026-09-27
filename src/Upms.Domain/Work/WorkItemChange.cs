namespace Upms.Domain.Work;

/// <summary>One append-only history entry of a work item (FR-031, constitution IV). Old and new values
/// are display snapshots (for example status names), so later renames do not rewrite history.</summary>
public sealed class WorkItemChange
{
    public const int NoteMaxLength = 200;

    private WorkItemChange()
    {
    }

    internal WorkItemChange(WorkItemField field, string? oldValue, string? newValue, string? note, ChangeContext context)
    {
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
        Note = note is { Length: > NoteMaxLength } ? note[..NoteMaxLength] : note;
        ActorId = context.ActorId;
        OccurredAt = context.At;
        ChangeSetId = context.ChangeSetId;
    }

    public long Id { get; private set; }

    public long WorkItemId { get; private set; }

    public Guid ChangeSetId { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public WorkItemField Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public string? Note { get; private set; }
}
