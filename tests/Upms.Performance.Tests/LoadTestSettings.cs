using System.Globalization;

namespace Upms.Performance.Tests;

/// <summary>Settings from environment variables, with the SC-002 values as defaults:
/// <list type="bullet">
/// <item><c>UPMS_PERF_CONNECTION</c>: an existing database to use (seeded if it has no projects); by default a
/// SQL Server container is started.</item>
/// <item><c>UPMS_PERF_WORK_ITEMS</c> (500000), <c>UPMS_PERF_USERS</c> (300 simulated users),
/// <c>UPMS_PERF_SECONDS</c> (120 seconds measured, after a 20-second warm-up).</item>
/// </list></summary>
public sealed record LoadTestSettings(string? ConnectionString, int WorkItems, int Users, TimeSpan Duration, TimeSpan WarmUp)
{
    public static LoadTestSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("UPMS_PERF_CONNECTION"),
        Number("UPMS_PERF_WORK_ITEMS", 500_000),
        Number("UPMS_PERF_USERS", 300),
        TimeSpan.FromSeconds(Number("UPMS_PERF_SECONDS", 120)),
        TimeSpan.FromSeconds(20));

    private static int Number(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
}
