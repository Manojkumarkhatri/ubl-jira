using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.Drawer;

/// <summary>History entries as sentences, for an item's history and the Summary page's recent activity (Jira parity pass
/// FR-022).</summary>
public static class HistoryText
{
    public static string Describe(ChangeView change, string itemNoun)
    {
        var note = string.IsNullOrEmpty(change.Note) ? "" : $" ({change.Note})";
        return change.Field switch
        {
            WorkItemField.Created => $"created the {itemNoun} in {change.NewValue}",
            WorkItemField.Title => $"changed the title from “{change.OldValue}” to “{change.NewValue}”",
            WorkItemField.Description when string.IsNullOrEmpty(change.OldValue) => "added a description",
            WorkItemField.Description when string.IsNullOrEmpty(change.NewValue) => "removed the description",
            WorkItemField.Description => "updated the description",
            WorkItemField.Priority => $"changed the priority from {change.OldValue} to {change.NewValue}",
            WorkItemField.Color => $"changed the color from {change.OldValue} to {change.NewValue}",
            WorkItemField.Status => $"changed the status from {change.OldValue} to {change.NewValue}{note}",
            WorkItemField.Rank => $"changed the order from position {change.OldValue} to {change.NewValue}{note}",
            WorkItemField.SubtaskAdded => $"added sub-task {change.NewValue}{(string.IsNullOrEmpty(change.Note) ? "" : $" “{change.Note}”")}",
            WorkItemField.CommentAdded => "added a comment",
            WorkItemField.CommentEdited => "edited a comment",
            WorkItemField.CommentDeleted => "deleted a comment",
            WorkItemField.Deleted => $"deleted the {itemNoun}",
            WorkItemField.Restored => $"restored the {itemNoun}",
            WorkItemField.Assignee when change.OldValue is null => $"assigned the {itemNoun} to {change.NewValue}",
            WorkItemField.Assignee when change.NewValue is null => $"removed the assignee {change.OldValue}",
            WorkItemField.Assignee => $"reassigned the {itemNoun} from {change.OldValue} to {change.NewValue}",
            WorkItemField.StartDate => DescribeDate("start date", change),
            WorkItemField.DueDate => DescribeDate("due date", change),
            WorkItemField.Type => $"changed the type from {TypeName(change.OldValue)} to {TypeName(change.NewValue)}{note}",
            WorkItemField.Container => (change.OldValue, change.NewValue) switch
            {
                (null, var into) => $"put the {itemNoun} in {into}",
                (var from, null) => $"took the {itemNoun} out of {from}",
                var (from, to) => $"moved the {itemNoun} from {from} to {to}",
            },
            WorkItemField.Estimate => (change.OldValue, change.NewValue) switch
            {
                (null, var set) => $"set the estimate to {set}",
                (var was, null) => $"cleared the estimate (was {was})",
                var (from, to) => $"changed the estimate from {from} to {to}",
            },
            WorkItemField.Sprint => (change.OldValue, change.NewValue) switch
            {
                (null, var into) => $"moved the {itemNoun} into {into}",
                (var from, null) => $"moved the {itemNoun} from {from} to the backlog",
                var (from, to) => $"moved the {itemNoun} from {from} to {to}",
            },
            WorkItemField.GateSetup => $"set up the gate: {change.NewValue}",
            WorkItemField.GateRequested => "requested approval of the gate",
            WorkItemField.GateWithdrawn => "withdrew the request for approval",
            WorkItemField.GateDecision => (change.NewValue == "Sent back" ? "sent the gate back" : "approved the gate")
                + (string.IsNullOrEmpty(change.Note) ? "" : $": “{change.Note}”"),
            WorkItemField.GateApproved => "gave the last approval needed: the gate is approved",
            WorkItemField.DependencyAdded => $"added a dependency: this {itemNoun} {change.NewValue}",
            WorkItemField.DependencyRemoved => $"removed a dependency: this {itemNoun} {change.OldValue}"
                + (change.Note switch { "deleted" => " (the other item was deleted)", "moved" => " (moved to another space)", _ => "" }),
            WorkItemField.AttachmentAdded => $"attached {change.NewValue}",
            WorkItemField.AttachmentRemoved => $"removed the attachment {change.OldValue}"
                + (change.Note == "deleted permanently" ? " and deleted it permanently" : ""),
            WorkItemField.LabelAdded => $"added the label {change.NewValue}",
            WorkItemField.LabelRemoved => $"removed the label {change.OldValue}",
            WorkItemField.WorkLogged => $"logged {change.NewValue}",
            WorkItemField.WorkLogChanged => $"changed logged work from {change.OldValue} to {change.NewValue}",
            WorkItemField.WorkLogDeleted => $"deleted a work log of {change.OldValue}",
            WorkItemField.OriginalEstimate => Estimate("original estimate", change),
            WorkItemField.RemainingEstimate => Estimate("remaining estimate", change),
            WorkItemField.Moved => $"moved the {itemNoun} from {change.OldValue} to {change.NewValue}",
            WorkItemField.Parent => (change.OldValue, change.NewValue) switch
            {
                (null, var parent) => $"set the parent to {parent}",
                (var parent, null) => $"removed the parent {parent}",
                var (from, to) => $"changed the parent from {from} to {to}",
            },
            WorkItemField.ClonedFrom => $"cloned the {itemNoun} from {change.OldValue}",
            WorkItemField.ClonedTo => $"cloned the {itemNoun} as {change.NewValue}",
            WorkItemField.CustomField => (change.OldValue, change.NewValue) switch
            {
                (null, var set) => $"set {change.Note} to {Short(set)}",
                (var was, null) => $"cleared {change.Note} (was {Short(was)})",
                var (from, to) => $"changed {change.Note} from {Short(from)} to {Short(to)}",
            },
            _ => $"changed {change.Field}",
        };
    }

    /// <summary>A long value in one line of history: its first 120 characters.</summary>
    private static string Short(string? value)
    {
        var line = (value ?? "").ReplaceLineEndings(" ");
        return line.Length <= 120 ? line : line[..120] + "…";
    }

    private static string Estimate(string name, ChangeView change) => (change.OldValue, change.NewValue) switch
    {
        (null, var set) => $"set the {name} to {set}",
        (var was, null) => $"cleared the {name} (was {was})",
        var (from, to) => $"changed the {name} from {from} to {to}",
    };

    // Types are stored by name ("Subtask") and shown as people read them ("Sub-task").
    private static string TypeName(string? stored) =>
        Enum.TryParse<WorkItemType>(stored, out var type) ? WorkItemTypes.DisplayName(type) : stored ?? "";

    // Dates are stored as ISO values and shown as "1 Oct 2026" (Phase 2 research R8).
    private static string DescribeDate(string name, ChangeView change) => (change.OldValue, change.NewValue) switch
    {
        (null, var added) => $"set the {name} to {ShowDate(added)}",
        (var removed, null) => $"cleared the {name} (was {ShowDate(removed)})",
        var (from, to) => $"changed the {name} from {ShowDate(from)} to {ShowDate(to)}",
    };

    private static string ShowDate(string? iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)
            ? date.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : iso ?? "";
}
