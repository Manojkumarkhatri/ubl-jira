namespace Upms.Application.Work;

/// <summary>Keeps card rank keys short (research R14); order is never changed and nothing is recorded in
/// history, because the cards' positions do not change.</summary>
public interface IRankRebalancer
{
    /// <summary>Rebalances every column (and sub-task list) that has a key over the threshold; returns how many.</summary>
    Task<int> RebalanceLongRanksAsync(CancellationToken ct);
}
