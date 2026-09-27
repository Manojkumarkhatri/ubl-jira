using Upms.Domain.Common;

namespace Upms.Domain.Projects;

/// <summary>A board column, which is also a status a work item can have (research R11).</summary>
public sealed class ProjectStatus
{
    public const int NameMaxLength = 30;
    public const int MinWipLimit = 1;
    public const int MaxWipLimit = 99;

    private ProjectStatus()
    {
    }

    internal ProjectStatus(string name, StatusCategory category, int position)
    {
        Name = name;
        NormalizedName = Project.NormalizeName(name);
        Category = category;
        Position = position;
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    /// <summary>1–30 characters (FR-034).</summary>
    public string Name { get; private set; } = "";

    /// <summary>Upper-invariant name; unique per project.</summary>
    public string NormalizedName { get; private set; } = "";

    public StatusCategory Category { get; private set; }

    /// <summary>0-based order on the board; consecutive per project.</summary>
    public int Position { get; private set; }

    /// <summary>Work-in-progress limit, 1–99 when set (FR-036).</summary>
    public int? WipLimit { get; private set; }

    internal void Rename(string name)
    {
        Name = name;
        NormalizedName = Project.NormalizeName(name);
    }

    internal void MoveTo(int position) => Position = position;

    internal void LimitTo(int? limit) => WipLimit = limit;

    internal void ChangeCategory(StatusCategory category) => Category = category;
}
