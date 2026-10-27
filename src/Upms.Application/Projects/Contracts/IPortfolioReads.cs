using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Projects.Contracts;

/// <summary>What an MIS covers (owner's decision of 2026-10-20).</summary>
public enum MisLevel
{
    Space,
    Portfolio,
    Department,
    All,
}

/// <summary>Which spaces an MIS covers, for the Work module that counts their work.</summary>
public interface IPortfolioReads
{
    /// <summary>The spaces of one space (<paramref name="spaceKey"/>), portfolio or department (<paramref name="id"/>), or of
    /// everything, when the caller has the tick for that level and the place is theirs: a space they can see, a portfolio
    /// they lead or that is in their department, their own department. Anything else is <c>NotFound</c> or
    /// <c>Forbidden</c>.</summary>
    Task<Result<MisScope>> ScopeAsync(MisLevel level, string? spaceKey, long? id, CancellationToken ct);
}

public sealed record MisScope(MisLevel Level, string Name, IReadOnlyList<ScopeSpace> Spaces);

/// <param name="Leads">The portfolio's leads, or the space's admins when it is in no portfolio.</param>
public sealed record ScopeSpace(long ProjectId, string Key, string Name, ProjectTemplate Template, string? Portfolio,
    string? Department, IReadOnlyList<string> Leads);
