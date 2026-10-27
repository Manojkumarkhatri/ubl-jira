using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Files on work items (Phase 4 FR-023–FR-031, research R12–R14). A file can be downloaded only once its malware
/// check has passed, and only by people who can see its item at that moment.</summary>
public interface IAttachmentService
{
    /// <summary>The item's files that were not removed, oldest first (View right).</summary>
    Task<Result<IReadOnlyList<AttachmentView>>> ListAsync(string workItemKey, CancellationToken ct);

    /// <summary>Every file on the space's live items, newest first, as Jira's space "Attachments" view (View right; at
    /// most <see cref="SpaceAttachments.MaxFiles"/>).</summary>
    Task<Result<SpaceAttachments>> ListForSpaceAsync(string spaceKey, CancellationToken ct);

    /// <summary>Contribute right. Checks the type, the size (25 MB) and that the content matches the type, then stores
    /// the file as "being checked" and records it in the item's history (<c>FileTypeNotAllowed</c>, <c>FileTooLarge</c>,
    /// <c>FileContentMismatch</c>).</summary>
    /// <param name="size">The size the browser reported; the bytes read are what count.</param>
    Task<Result<AttachmentView>> AddAsync(string workItemKey, string fileName, long size, Stream content, CancellationToken ct);

    /// <summary>The uploader while they can contribute, Project Admins and administrators; the bytes are kept
    /// (<c>AttachmentNotAvailable</c> once removed).</summary>
    Task<Result> RemoveAsync(long attachmentId, CancellationToken ct);

    /// <summary>Administrators only: deletes the bytes for good, audited as <c>AttachmentPurged</c> (FR-029).</summary>
    Task<Result> PurgeAsync(long attachmentId, CancellationToken ct);

    /// <summary>The file to download, or with <paramref name="preview"/> an image to show; <c>NotFound</c> for anything
    /// the caller may not have now (FR-027). The caller disposes the content.</summary>
    Task<Result<AttachmentFile>> OpenAsync(long attachmentId, bool preview, CancellationToken ct);
}

/// <param name="CanRemove">The caller may remove it now.</param>
/// <param name="CanPurge">The caller is an administrator.</param>
public sealed record AttachmentView(long Id, string FileName, long Size, string UploadedBy, DateTimeOffset UploadedAt,
    AttachmentCheckStatus Status, bool IsImage, bool CanRemove, bool CanPurge = false);

public sealed record SpaceAttachments(string SpaceKey, string SpaceName, Upms.Domain.Common.ProjectTemplate Template, bool CanManage,
    bool CanContribute, bool CanRestoreDeleted, IReadOnlyList<SpaceAttachment> Files)
{
    public const int MaxFiles = 500;
}

/// <param name="File">The file as the drawer shows it.</param>
public sealed record SpaceAttachment(AttachmentView File, string ItemKey, string ItemTitle, WorkItemType ItemType);

public sealed record AttachmentFile(string FileName, string ContentType, long Size, Stream Content);

/// <summary>Checks files waiting for their malware check; run every few seconds by a background worker (research R14).</summary>
public interface IAttachmentScanner
{
    /// <returns>How many files were checked or retried.</returns>
    Task<int> ScanDueAsync(CancellationToken ct);
}

/// <summary>A malware scanner: ClamAV through its daemon in production (research R14), a fake in tests.</summary>
public interface IMalwareScanner
{
    Task<ScanOutcome> ScanAsync(Stream content, CancellationToken ct);
}

public enum ScanVerdict
{
    Clean,
    Infected,

    /// <summary>The scanner could not be reached or answered with an error; the file stays "being checked".</summary>
    Unavailable,
}

public sealed record ScanOutcome(ScanVerdict Verdict, string? Detail = null)
{
    public static ScanOutcome Clean { get; } = new(ScanVerdict.Clean);

    public static ScanOutcome Infected(string threat) => new(ScanVerdict.Infected, threat);

    public static ScanOutcome Unavailable(string reason) => new(ScanVerdict.Unavailable, reason);
}

/// <summary>The files' bytes, kept in their own table and streamed in and out (research R12).</summary>
public interface IAttachmentContentStore
{
    /// <summary>Writes the bytes in the caller's transaction.</summary>
    Task WriteAsync(long attachmentId, Stream content, CancellationToken ct);

    /// <summary>The bytes as a stream read from the database as it goes; the caller disposes it.</summary>
    Task<Stream?> OpenReadAsync(long attachmentId, CancellationToken ct);

    Task DeleteAsync(long attachmentId, CancellationToken ct);
}
