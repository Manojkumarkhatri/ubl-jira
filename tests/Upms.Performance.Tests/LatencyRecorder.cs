using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Upms.Performance.Tests;

public enum Outcome
{
    Ok,
    Conflict,
    Failed,
}

/// <summary>Response times per operation, recorded from many simulated users at once.</summary>
public sealed class LatencyRecorder
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<(TimeSpan Elapsed, Outcome Outcome)>> _samples = new(StringComparer.Ordinal);

    public void Record(string operation, TimeSpan elapsed, Outcome outcome) =>
        _samples.GetOrAdd(operation, _ => []).Add((elapsed, outcome));

    public IReadOnlyList<OperationStats> Summaries() => _samples
        .OrderBy(s => s.Key, StringComparer.Ordinal)
        .Select(s =>
        {
            var samples = s.Value.ToArray();
            var sorted = samples.Select(x => x.Elapsed.TotalMilliseconds).Order().ToArray();
            return new OperationStats(s.Key, samples.Length, samples.Count(x => x.Outcome == Outcome.Conflict),
                samples.Count(x => x.Outcome == Outcome.Failed), Percentile(sorted, 0.50), Percentile(sorted, 0.95),
                Percentile(sorted, 0.99), sorted.Length == 0 ? 0 : sorted[^1]);
        })
        .ToList();

    public static string Report(IEnumerable<OperationStats> stats)
    {
        var report = new StringBuilder();
        report.AppendLine("| Operation | Calls | Conflicts | Failures | p50 ms | p95 ms | p99 ms | max ms |");
        report.AppendLine("|-----------|------:|----------:|---------:|-------:|-------:|-------:|-------:|");
        foreach (var s in stats)
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"| {s.Operation} | {s.Calls} | {s.Conflicts} | {s.Failures} | {s.P50:F0} | {s.P95:F0} | {s.P99:F0} | {s.Max:F0} |");
        }

        return report.ToString();
    }

    private static double Percentile(double[] sorted, double fraction) =>
        sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Length) - 1, 0, sorted.Length - 1)];
}

public sealed record OperationStats(string Operation, int Calls, int Conflicts, int Failures, double P50, double P95, double P99, double Max);
