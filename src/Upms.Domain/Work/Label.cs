using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A label shared across the organization (Phase 4 FR-042, FR-043, research R18): one row per name, matched
/// regardless of letter case, so the first spelling used is the one everyone sees.</summary>
public sealed class Label
{
    public const int NameMaxLength = 50;
    public const int MaxPerItem = 20;
    public const string InvalidLabelCode = "InvalidLabel";
    public const string TooManyLabelsCode = "TooManyLabels";

    private Label()
    {
    }

    public int Id { get; private set; }

    /// <summary>The first spelling used.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Upper-cased in the invariant culture; unique (FR-043).</summary>
    public string NormalizedName { get; private set; } = "";

    public static string Normalize(string? name) => (name ?? "").Trim().ToUpperInvariant();

    public static DomainResult<Label> Create(string name)
    {
        if (Check(name) is { } invalid)
        {
            return invalid;
        }

        return new Label { Name = name.Trim(), NormalizedName = Normalize(name) };
    }

    /// <summary>1–50 characters with no whitespace, control characters or commas (FR-042); commas separate labels in
    /// addresses (contracts/ui-routes.md).</summary>
    public static DomainError? Check(string? name)
    {
        var trimmed = (name ?? "").Trim();
        return trimmed.Length is 0 or > NameMaxLength || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == ',')
            ? new DomainError(InvalidLabelCode, "A label can't contain spaces or commas and can have up to 50 characters.", "Label")
            : null;
    }

    /// <summary>Whether an item with <paramref name="labelsOnItem"/> labels can take one more (FR-042).</summary>
    public static DomainError? CheckRoom(int labelsOnItem) => labelsOnItem >= MaxPerItem
        ? DomainError.Rule(TooManyLabelsCode, $"An item can have up to {MaxPerItem} labels.")
        : null;
}

/// <summary>A label on a work item, with who added it and when. Adding and removing are recorded in the item's history
/// (<see cref="WorkItemField.LabelAdded"/>, <see cref="WorkItemField.LabelRemoved"/>).</summary>
public sealed class WorkItemLabel
{
    private WorkItemLabel()
    {
    }

    public long WorkItemId { get; private set; }

    public int LabelId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public Guid AddedById { get; private set; }

    public static WorkItemLabel Add(long workItemId, int labelId, Guid addedById, DateTimeOffset now) =>
        new() { WorkItemId = workItemId, LabelId = labelId, AddedById = addedById, AddedAt = now };
}
