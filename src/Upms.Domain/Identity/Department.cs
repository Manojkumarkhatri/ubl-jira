using Upms.Domain.Common;

namespace Upms.Domain.Identity;

/// <summary>A part of the organization, such as IT-PMO or Finance (owner's decision of 2026-10-20). People belong to one;
/// its Heads see all of its portfolios and spaces, and other departments are walled off from it.</summary>
public sealed class Department
{
    public const int NameMaxLength = 80;

    private Department()
    {
    }

    public long Id { get; private set; }

    public string Name { get; private set; } = "";

    /// <summary>Upper-invariant name; department names are unique ignoring case.</summary>
    public string NormalizedName { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    public static DomainResult<Department> Create(string name, DateTimeOffset now)
    {
        var department = new Department { CreatedAt = now };
        if (department.Rename(name) is { } error)
        {
            return error;
        }

        return department;
    }

    public DomainError? Rename(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            return DomainError.Invalid("Name", $"Enter a name of up to {NameMaxLength} characters.");
        }

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        return null;
    }
}
