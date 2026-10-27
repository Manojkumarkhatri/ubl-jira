using Upms.Domain.Common;

namespace Upms.Domain.Projects;

/// <summary>A group of spaces in one department, such as "Data &amp; AI" in IT-PMO (owner's decision of 2026-10-20). Its
/// leads see and manage every space in it and download its MIS.</summary>
public sealed class Portfolio
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 500;

    private Portfolio()
    {
    }

    public long Id { get; private set; }

    /// <summary>The department it belongs to (the Identity module's department).</summary>
    public long DepartmentId { get; private set; }

    public string Name { get; private set; } = "";

    /// <summary>Upper-invariant name; unique within the department, ignoring case.</summary>
    public string NormalizedName { get; private set; } = "";

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DomainResult<Portfolio> Create(long departmentId, string name, string? description, DateTimeOffset now)
    {
        var portfolio = new Portfolio { DepartmentId = departmentId, CreatedAt = now };
        if (portfolio.Edit(name, description) is { } error)
        {
            return error;
        }

        return portfolio;
    }

    public DomainError? Edit(string name, string? description)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            return DomainError.Invalid("Name", $"Enter a name of up to {NameMaxLength} characters.");
        }

        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text is { Length: > DescriptionMaxLength })
        {
            return DomainError.Invalid("Description", $"Keep the description to {DescriptionMaxLength} characters.");
        }

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        Description = text;
        return null;
    }
}

/// <summary>A person who leads a portfolio.</summary>
public sealed class PortfolioLead
{
    private PortfolioLead()
    {
    }

    public PortfolioLead(long portfolioId, Guid userId)
    {
        PortfolioId = portfolioId;
        UserId = userId;
    }

    public long PortfolioId { get; private set; }

    public Guid UserId { get; private set; }
}
