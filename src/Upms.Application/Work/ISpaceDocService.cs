using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>A space's Docs, as Jira's space Docs: pages of formatted text the team keeps beside its work. Everyone in the
/// space reads them; people who can contribute write them; the author or a Space admin deletes one.</summary>
public interface ISpaceDocService
{
    /// <summary>The space's pages, last changed first (View right).</summary>
    Task<Result<SpaceDocList>> ListAsync(string spaceKey, CancellationToken ct);

    Task<Result<SpaceDocView>> GetAsync(string spaceKey, long docId, CancellationToken ct);

    /// <summary>A new, empty page (Contribute right).</summary>
    Task<Result<SpaceDocView>> CreateAsync(string spaceKey, string title, CancellationToken ct);

    /// <summary>Saves the title and the body, the editor's JSON document (Contribute right).</summary>
    Task<Result<SpaceDocView>> SaveAsync(string spaceKey, long docId, string title, string? document, CancellationToken ct);

    /// <summary>Its author while they can contribute, or a Space admin.</summary>
    Task<Result> DeleteAsync(string spaceKey, long docId, CancellationToken ct);
}

public sealed record SpaceDocList(bool CanCreate, IReadOnlyList<SpaceDocRow> Docs);

/// <param name="Preview">The start of the page's text.</param>
public sealed record SpaceDocRow(long Id, string Title, string? Preview, string UpdatedBy, DateTimeOffset UpdatedAt);

public sealed record SpaceDocView(long Id, string Title, RichText? Body, string CreatedBy, DateTimeOffset CreatedAt, string UpdatedBy,
    DateTimeOffset UpdatedAt, bool CanEdit, bool CanDelete);
