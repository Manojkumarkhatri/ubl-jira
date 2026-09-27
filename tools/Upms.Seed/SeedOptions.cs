namespace Upms.Seed;

/// <summary>How much data to generate. <see cref="WorkItems"/> counts tasks and sub-tasks together.</summary>
public sealed record SeedOptions(int Users, int Projects, int WorkItems, int RandomSeed = 20260927)
{
    /// <summary>Every seeded user signs in with this password.</summary>
    public const string Password = "seeded passphrase 2026";

    /// <summary>The volumes of the performance baseline (SC-002): 2,000 users, 1,000 projects, 500,000 work items.</summary>
    public static SeedOptions Baseline { get; } = new(2_000, 1_000, 500_000);

    /// <summary>Top-level tasks in the largest project; about 40% of them are open, so its board shows about 500 cards.</summary>
    public int LargestProjectTasks => Math.Min(1_200, Math.Max(10, WorkItems / 10));
}

/// <summary>What was generated, for the performance suite to pick subjects from.</summary>
public sealed record SeedSummary(
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<string> ProjectKeys,
    string LargestProjectKey,
    int WorkItems,
    int Changes,
    int Comments,
    TimeSpan Elapsed);
