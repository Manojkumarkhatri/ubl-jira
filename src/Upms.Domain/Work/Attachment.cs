using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>Where an attachment's malware check stands (Phase 4 FR-025).</summary>
public enum AttachmentCheckStatus
{
    /// <summary>Uploaded and waiting for the scanner; not downloadable.</summary>
    Checking,

    Passed,

    /// <summary>Found infected: quarantined, never served, kept until an administrator purges it.</summary>
    Blocked,
}

/// <summary>A file attached to a work item (Phase 4 FR-023–FR-030, data model, research R12–R14). Its bytes are kept in
/// their own table; it can be downloaded only once its check has passed, and while it is neither removed nor purged.</summary>
public sealed class Attachment
{
    private Attachment()
    {
    }

    public long Id { get; private set; }

    public long WorkItemId { get; private set; }

    public string FileName { get; private set; } = "";

    /// <summary>Lower case, without the dot.</summary>
    public string Extension { get; private set; } = "";

    public string ContentType { get; private set; } = "";

    public long Size { get; private set; }

    public byte[] Sha256 { get; private set; } = [];

    public Guid UploadedById { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public AttachmentCheckStatus CheckStatus { get; private set; }

    /// <summary>Scans tried, successful or not.</summary>
    public int CheckAttempts { get; private set; }

    /// <summary>When the scan worker tries next; null once checked.</summary>
    public DateTimeOffset? NextCheckAt { get; private set; }

    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>The scanner's signature name, for a blocked file.</summary>
    public string? ThreatName { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedById { get; private set; }

    public DateTimeOffset? PurgedAt { get; private set; }

    public Guid? PurgedById { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsImage => AttachmentRules.IsImage(Extension);

    /// <summary>Downloadable: checked and clean, neither removed nor purged (FR-025, FR-027).</summary>
    public bool IsAvailable => CheckStatus == AttachmentCheckStatus.Passed && RemovedAt is null && PurgedAt is null;

    /// <summary>A new upload, waiting for its malware check; the content was checked against its type first.</summary>
    public static DomainResult<Attachment> Upload(long workItemId, string fileName, long size, byte[] sha256, Guid uploaderId,
        DateTimeOffset now)
    {
        var name = AttachmentRules.CleanFileName(fileName);
        if ((AttachmentRules.CheckType(name) ?? AttachmentRules.CheckSize(size)) is { } refused)
        {
            return refused;
        }

        var extension = AttachmentRules.ExtensionOf(name);
        return new Attachment
        {
            WorkItemId = workItemId,
            FileName = name,
            Extension = extension,
            ContentType = AttachmentRules.ContentTypeOf(extension),
            Size = size,
            Sha256 = sha256,
            UploadedById = uploaderId,
            UploadedAt = now,
            CheckStatus = AttachmentCheckStatus.Checking,
            NextCheckAt = now,
        };
    }

    /// <summary>A copy of a file for a cloned item (Jira's "Clone" with attachments): the same bytes, so a clean file stays
    /// clean and one still being checked is checked again. Blocked files are never copied (the caller skips them).</summary>
    public static Attachment CopyOf(Attachment source, long workItemId, Guid copiedById, DateTimeOffset now) => new()
    {
        WorkItemId = workItemId,
        FileName = source.FileName,
        Extension = source.Extension,
        ContentType = source.ContentType,
        Size = source.Size,
        Sha256 = source.Sha256,
        UploadedById = copiedById,
        UploadedAt = now,
        CheckStatus = source.CheckStatus == AttachmentCheckStatus.Passed ? AttachmentCheckStatus.Passed : AttachmentCheckStatus.Checking,
        CheckedAt = source.CheckStatus == AttachmentCheckStatus.Passed ? source.CheckedAt : null,
        NextCheckAt = source.CheckStatus == AttachmentCheckStatus.Passed ? null : now,
    };

    public void Pass(DateTimeOffset now) => Finish(AttachmentCheckStatus.Passed, null, now);

    /// <summary>Quarantined: the bytes stay, but the file is never served or checked again (research R14).</summary>
    public void Block(string threatName, DateTimeOffset now) =>
        Finish(AttachmentCheckStatus.Blocked, threatName.Length <= 200 ? threatName : threatName[..200], now);

    /// <summary>The scanner could not check the file: it stays "being checked" and is tried again after 1, then 5, then
    /// every 15 minutes (SC-003: it fails closed).</summary>
    public void CheckFailed(DateTimeOffset now)
    {
        CheckAttempts++;
        NextCheckAt = now.AddMinutes(CheckAttempts switch { 1 => 1, 2 => 5, _ => 15 });
    }

    /// <summary>Removes a checking or passed file from its item; the record and bytes stay (FR-028).</summary>
    public DomainError? Remove(Guid actorId, DateTimeOffset now)
    {
        if (RemovedAt is not null || PurgedAt is not null || CheckStatus == AttachmentCheckStatus.Blocked)
        {
            return NotAvailable();
        }

        RemovedAt = now;
        RemovedById = actorId;
        return null;
    }

    /// <summary>Deletes the file permanently: the caller removes its bytes (FR-029).</summary>
    public DomainError? Purge(Guid actorId, DateTimeOffset now)
    {
        if (PurgedAt is not null)
        {
            return NotAvailable();
        }

        PurgedAt = now;
        PurgedById = actorId;
        NextCheckAt = null;
        return null;
    }

    private void Finish(AttachmentCheckStatus status, string? threat, DateTimeOffset now)
    {
        CheckStatus = status;
        ThreatName = threat;
        CheckAttempts++;
        CheckedAt = now;
        NextCheckAt = null;
    }

    private static DomainError NotAvailable() =>
        DomainError.Rule(AttachmentRules.NotAvailableCode, "This file has already been removed or deleted.");
}
