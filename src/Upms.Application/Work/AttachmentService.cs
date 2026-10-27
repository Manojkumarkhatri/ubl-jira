using System.Buffers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Files on work items (Phase 4 FR-023–FR-031, research R12, R13). Uploads are copied to a temporary file while
/// their size is counted and their hash computed, checked against their type, then streamed into the database "being
/// checked"; the scan worker decides whether they can ever be downloaded.</summary>
internal sealed class AttachmentService(
    IAppDbContext db,
    IProjectAccess access,
    IUserDirectory users,
    IAttachmentContentStore store,
    IContentAuditLog audit,
    TimeProvider time) : IAttachmentService
{
    public async Task<Result<IReadOnlyList<AttachmentView>>> ListAsync(string workItemKey, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.View, ct);
        return allowed.IsSuccess ? Result<IReadOnlyList<AttachmentView>>.Ok(await ViewsAsync(item.Id, allowed.Value!, ct)) : allowed.Error!;
    }

    public async Task<Result<SpaceAttachments>> ListForSpaceAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var rows = await (from a in db.Attachments.AsNoTracking()
                          join w in db.WorkItems.AsNoTracking() on a.WorkItemId equals w.Id
                          where w.ProjectId == caller.ProjectId && a.RemovedAt == null && a.PurgedAt == null
                          orderby a.UploadedAt descending, a.Id descending
                          select new { Attachment = a, w.Key, w.Title, w.Type })
            .Take(SpaceAttachments.MaxFiles)
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Select(r => r.Attachment.UploadedById).Distinct().ToList(), ct);
        var space = await db.Projects.AsNoTracking().Where(p => p.Id == caller.ProjectId).Select(p => new { p.Key, p.Name }).SingleAsync(ct);
        return new SpaceAttachments(space.Key, space.Name, caller.Template, caller.CanManage, caller.CanContribute, caller.IsAdministrator,
            rows.ConvertAll(r => new SpaceAttachment(View(r.Attachment, names, caller), r.Key, r.Title, r.Type)));
    }

    /// <summary>The item's listed files as the caller sees them (also for the drawer's details).</summary>
    public async Task<IReadOnlyList<AttachmentView>> ViewsAsync(long workItemId, ProjectAccessInfo caller, CancellationToken ct)
    {
        var files = await db.Attachments.AsNoTracking()
            .Where(a => a.WorkItemId == workItemId && a.RemovedAt == null && a.PurgedAt == null)
            .OrderBy(a => a.UploadedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);
        var names = await users.GetAsync(files.Select(a => a.UploadedById).Distinct().ToList(), ct);
        return files.ConvertAll(a => View(a, names, caller));
    }

    public async Task<Result<AttachmentView>> AddAsync(string workItemKey, string fileName, long size, Stream content, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var name = AttachmentRules.CleanFileName(fileName);
        if ((AttachmentRules.CheckType(name) ?? (size > AttachmentRules.MaxSize ? AttachmentRules.CheckSize(size) : null)) is { } refused)
        {
            return refused.ToAppError();
        }

        await using var buffer = new FileStream(Path.Combine(Path.GetTempPath(), $"upms-upload-{Guid.NewGuid():N}"), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None, 81_920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var copied = await CopyAsync(content, buffer, ct);
        if (copied is null)
        {
            return AttachmentRules.CheckSize(AttachmentRules.MaxSize + 1)!.ToAppError();
        }

        var (length, hash) = copied.Value;
        if ((AttachmentRules.CheckSize(length) ?? AttachmentRules.CheckContent(AttachmentRules.ExtensionOf(name), buffer)) is { } mismatch)
        {
            return mismatch.ToAppError();
        }

        var caller = allowed.Value!;
        var now = time.GetUtcNow();
        var created = Attachment.Upload(item.Id, name, length, hash, caller.UserId, now);
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var attachment = created.Value!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Attachments.Add(attachment);
        item.RecordActivity(WorkItemField.AttachmentAdded, null, attachment.FileName, ChangeContext.New(caller.UserId, now));
        await db.SaveChangesAsync(ct);
        buffer.Position = 0;
        await store.WriteAsync(attachment.Id, buffer, ct);
        await transaction.CommitAsync(ct);
        return View(attachment, await users.GetAsync([caller.UserId], ct), caller);
    }

    public async Task<Result> RemoveAsync(long attachmentId, CancellationToken ct)
    {
        var (attachment, item, error) = await LoadAsync(attachmentId, ct);
        if (error is not null)
        {
            return error;
        }

        var allowed = await access.RequireAsync(item!.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (attachment!.UploadedById != caller.UserId && !caller.CanManage)
        {
            return AppError.Forbidden("Only the person who attached a file, someone who manages the space or an Admin can remove it.");
        }

        var now = time.GetUtcNow();
        if (attachment.Remove(caller.UserId, now) is { } refused)
        {
            return refused.ToAppError();
        }

        item.RecordActivity(WorkItemField.AttachmentRemoved, attachment.FileName, null, ChangeContext.New(caller.UserId, now));
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> PurgeAsync(long attachmentId, CancellationToken ct)
    {
        var (attachment, item, error) = await LoadAsync(attachmentId, ct);
        if (error is not null)
        {
            return error;
        }

        var allowed = await access.RequireAsync(item!.ProjectId, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (!caller.IsAdministrator)
        {
            return AppError.Forbidden("Only administrators can delete a file permanently.");
        }

        var now = time.GetUtcNow();
        var wasListed = attachment!.RemovedAt is null;
        if (attachment.Purge(caller.UserId, now) is { } refused)
        {
            return refused.ToAppError();
        }

        if (wasListed)
        {
            item.RecordActivity(WorkItemField.AttachmentRemoved, attachment.FileName, null, ChangeContext.New(caller.UserId, now),
                "deleted permanently");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await store.DeleteAsync(attachment.Id, ct);
        var uploader = (await users.GetAsync([attachment.UploadedById], ct)).GetValueOrDefault(attachment.UploadedById)?.DisplayName;
        await audit.WriteAsync(ContentAuditEvent.AttachmentPurged, item.Key,
            new { attachment.FileName, attachment.Size, UploadedBy = uploader }, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<AttachmentFile>> OpenAsync(long attachmentId, bool preview, CancellationToken ct)
    {
        // Every refusal is "not found", so nobody learns whether a file exists (FR-027).
        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attachmentId && a.CheckStatus == AttachmentCheckStatus.Passed
                && a.RemovedAt == null && a.PurgedAt == null, ct);
        var projectId = attachment is null
            ? null
            : await db.WorkItems.AsNoTracking().Where(w => w.Id == attachment.WorkItemId).Select(w => (long?)w.ProjectId).SingleOrDefaultAsync(ct);
        if (attachment is null || projectId is null || (preview && !attachment.IsImage)
            || !(await access.RequireAsync(projectId.Value, ProjectRight.View, ct)).IsSuccess)
        {
            return AppError.NotFound("file");
        }

        var content = await store.OpenReadAsync(attachment.Id, ct);
        return content is null
            ? AppError.NotFound("file")
            : new AttachmentFile(attachment.FileName, attachment.ContentType, attachment.Size, content);
    }

    private static AttachmentView View(Attachment a, IReadOnlyDictionary<Guid, UserDisplay> names, ProjectAccessInfo caller) =>
        new(a.Id, a.FileName, a.Size, WorkItemReads.NameOf(names, a.UploadedById), a.UploadedAt, a.CheckStatus, a.IsImage,
            CanRemove: caller.CanContribute && a.CheckStatus != AttachmentCheckStatus.Blocked
                && (a.UploadedById == caller.UserId || caller.CanManage),
            CanPurge: caller.IsAdministrator);

    /// <summary>Copies the upload, counting its bytes and hashing them; null as soon as it passes 25 MB.</summary>
    private static async Task<(long Length, byte[] Hash)?> CopyAsync(Stream content, Stream buffer, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var chunk = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            long length = 0;
            int read;
            while ((read = await content.ReadAsync(chunk.AsMemory(0, 81_920), ct)) > 0)
            {
                length += read;
                if (length > AttachmentRules.MaxSize)
                {
                    return null;
                }

                hash.AppendData(chunk, 0, read);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }

            await buffer.FlushAsync(ct);
            return (length, hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private async Task<(Attachment? Attachment, WorkItem? Item, AppError? Error)> LoadAsync(long attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.SingleOrDefaultAsync(a => a.Id == attachmentId && a.PurgedAt == null, ct);
        var item = attachment is null ? null : await db.WorkItems.SingleOrDefaultAsync(w => w.Id == attachment.WorkItemId, ct);
        return attachment is null || item is null ? (null, null, AppError.NotFound("file")) : (attachment, item, null);
    }
}
