using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;

namespace Upms.Application.Work;

/// <summary>The MIS: a management report of a space, a portfolio, a department or everything (owner's decision of
/// 2026-10-20), downloaded as Excel or PDF. Who may download which level is checked by <see cref="IPortfolioReads"/>.</summary>
public interface IMisService
{
    /// <param name="today">The viewer's today, for what is late and due soon.</param>
    Task<Result<MisReport>> BuildAsync(MisLevel level, string? spaceKey, long? id, DateOnly today, CancellationToken ct);
}

/// <param name="GeneratedBy">The person who downloaded it.</param>
public sealed record MisReport(
    MisLevel Level,
    string Scope,
    DateOnly Today,
    DateTimeOffset GeneratedAt,
    string GeneratedBy,
    MisTotals Totals,
    IReadOnlyList<MisSpaceRow> Spaces,
    IReadOnlyList<MisContainerRow> Containers,
    IReadOnlyList<MisItemRow> Overdue,
    IReadOnlyList<MisItemRow> Milestones,
    IReadOnlyList<MisPersonRow> People)
{
    /// <summary>"Space", "Portfolio", "Department" or "Organization".</summary>
    public string LevelName => Level switch
    {
        MisLevel.Space => "Space",
        MisLevel.Portfolio => "Portfolio",
        MisLevel.Department => "Department",
        _ => "Organization",
    };
}

/// <param name="Items">Work items other than epics, stages and milestones.</param>
/// <param name="DueSoon">Open items due within <see cref="MisRules.DueSoonDays"/> days.</param>
public sealed record MisTotals(int Spaces, int Items, int ToDo, int InProgress, int Done, int Overdue, int DueSoon, int Milestones,
    int OnTrack, int AtRisk, int OffTrack)
{
    public int PercentDone => Items == 0 ? 0 : (int)Math.Round(Done * 100.0 / Items);
}

public sealed record MisSpaceRow(
    string? Department,
    string? Portfolio,
    string Key,
    string Name,
    ProjectTemplate Template,
    string Leads,
    int Items,
    int ToDo,
    int InProgress,
    int Done,
    int Overdue,
    int DueSoon,
    string? NextMilestone,
    DateOnly? NextMilestoneDate,
    MisHealth Health,
    DateTimeOffset? LastUpdated)
{
    public int PercentDone => Items == 0 ? 0 : (int)Math.Round(Done * 100.0 / Items);
}

/// <summary>An epic or a stage with its progress.</summary>
public sealed record MisContainerRow(string Space, string Key, string Title, string Type, string Status, StatusCategory Category,
    string? Owner, DateOnly? Start, DateOnly? Due, int Items, int Done, bool Late)
{
    public int PercentDone => Items == 0 ? 0 : (int)Math.Round(Done * 100.0 / Items);
}

/// <param name="Days">Days late (overdue items) or days left (milestones; negative when late).</param>
public sealed record MisItemRow(string Space, string Key, string Type, string Title, string Status, StatusCategory Category,
    string? Assignee, DateOnly? Due, int Days, string Priority);

public sealed record MisPersonRow(string Name, int Open, int Overdue, int DoneLast30Days);

public enum MisHealth
{
    NoWork,
    OnTrack,
    AtRisk,
    OffTrack,
}

public static class MisRules
{
    public const int DueSoonDays = 14;
    public const int MilestoneWindowDays = 60;

    /// <summary>The most rows of a list the report holds; the counts above it cover everything.</summary>
    public const int MaxRows = 2000;

    public static string Name(this MisHealth health) => health switch
    {
        MisHealth.OnTrack => "On track",
        MisHealth.AtRisk => "At risk",
        MisHealth.OffTrack => "Off track",
        _ => "No work",
    };

    /// <summary>Off track: a fifth or more of the open work is late, or a milestone is late. At risk: anything late, or a
    /// milestone due within a week with the work still open. On track otherwise.</summary>
    public static MisHealth Health(int items, int open, int overdue, bool lateMilestone, bool milestoneThisWeek)
    {
        if (items == 0 && !lateMilestone && !milestoneThisWeek)
        {
            return MisHealth.NoWork;
        }

        if (lateMilestone || (open > 0 && overdue * 5 >= open && overdue > 0))
        {
            return MisHealth.OffTrack;
        }

        return overdue > 0 || milestoneThisWeek ? MisHealth.AtRisk : MisHealth.OnTrack;
    }
}
