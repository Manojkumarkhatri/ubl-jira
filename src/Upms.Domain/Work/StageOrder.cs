using Upms.Domain.Common;

namespace Upms.Domain.Work;

public enum StagePlace
{
    /// <summary>Its gate is approved.</summary>
    Approved,

    /// <summary>The first stage whose gate is not approved.</summary>
    Current,

    /// <summary>After the current stage: its work cannot start (FR-032).</summary>
    Locked,
}

/// <summary>A stage as its project's order sees it.</summary>
public sealed record StageSlot(long Id, string Key, string Title, string Rank, bool Approved);

/// <summary>The order of a Stage-gate project's live stages, by rank then ID (Phase 3 FR-031, FR-032, FR-039; research
/// R13): the current stage is the first whose gate is not approved and the stages after it are locked. Approved stages
/// and the current stage keep their places; locked stages move among themselves, and new stages go after the current
/// one.</summary>
public sealed class StageOrder
{
    public const string FixedCode = "StageOrderFixed";

    public StageOrder(IEnumerable<StageSlot> stages)
    {
        Stages = [.. stages.OrderBy(s => s.Rank, StringComparer.Ordinal).ThenBy(s => s.Id)];
        var current = Stages.ToList().FindIndex(s => !s.Approved);
        CurrentIndex = current;
    }

    public IReadOnlyList<StageSlot> Stages { get; }

    public StageSlot? Current => CurrentIndex >= 0 ? Stages[CurrentIndex] : null;

    private int CurrentIndex { get; }

    public StagePlace PlaceOf(long stageId)
    {
        var index = IndexOf(stageId);
        return CurrentIndex < 0 || index < CurrentIndex ? StagePlace.Approved : index == CurrentIndex ? StagePlace.Current : StagePlace.Locked;
    }

    public bool IsLocked(long stageId) => PlaceOf(stageId) == StagePlace.Locked;

    /// <summary>The stage whose gate must be approved before a locked stage opens: the current stage.</summary>
    public StageSlot? BlockingGateFor(long stageId) => IsLocked(stageId) ? Current : null;

    /// <summary>Where a new stage goes: after <paramref name="afterStageId"/>, which must be the current stage or a locked
    /// one, or at the end. Gives the neighbouring ranks for <see cref="Rank.Between"/>.</summary>
    public DomainError? CheckAddAfter(long? afterStageId, out string? before, out string? after)
    {
        before = after = null;
        if (afterStageId is not { } id)
        {
            before = Stages.Count > 0 ? Stages[^1].Rank : null;
            return null;
        }

        var index = IndexOf(id);
        if (index < 0 || PlaceOf(id) == StagePlace.Approved)
        {
            return Fixed();
        }

        before = Stages[index].Rank;
        after = index + 1 < Stages.Count ? Stages[index + 1].Rank : null;
        return null;
    }

    /// <summary>Moves a locked stage up (-1) or down (+1) among the locked stages. Gives the neighbouring ranks of its new
    /// place.</summary>
    public DomainError? CheckMove(long stageId, int offset, out string? before, out string? after)
    {
        before = after = null;
        var index = IndexOf(stageId);
        var target = index + offset;
        if (index < 0 || !IsLocked(stageId) || offset is not (-1 or 1) || target <= CurrentIndex || target >= Stages.Count)
        {
            return Fixed();
        }

        // Moving up puts the stage between the two stages before it; moving down, between the two after it.
        (before, after) = offset < 0
            ? (Stages[target - 1].Rank, Stages[target].Rank)
            : (Stages[target].Rank, target + 1 < Stages.Count ? Stages[target + 1].Rank : null);
        return null;
    }

    private int IndexOf(long stageId) => Stages.ToList().FindIndex(s => s.Id == stageId);

    private static DomainError Fixed() => DomainError.Rule(FixedCode,
        "Approved stages and the current stage keep their places; locked stages move only among themselves.");
}
