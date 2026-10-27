using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Work;

internal sealed class SpaceDocService(IAppDbContext db, IProjectAccess access, IUserDirectory users, TimeProvider time) : ISpaceDocService
{
    public const int PreviewLength = 160;
    public const int MaxDocs = 500;

    public async Task<Result<SpaceDocList>> ListAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var rows = await db.SpaceDocs.AsNoTracking()
            .Where(d => d.ProjectId == caller.ProjectId)
            .OrderByDescending(d => d.UpdatedAt).ThenByDescending(d => d.Id)
            .Take(MaxDocs)
            .Select(d => new { d.Id, d.Title, d.BodyText, d.UpdatedById, d.UpdatedAt })
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Select(r => r.UpdatedById).Distinct().ToList(), ct);
        return new SpaceDocList(caller.CanContribute, rows.ConvertAll(r => new SpaceDocRow(r.Id, r.Title,
            r.BodyText is { } text ? (text.Length > PreviewLength ? text[..PreviewLength] + "…" : text).ReplaceLineEndings(" ") : null,
            WorkItemReads.NameOf(names, r.UpdatedById), r.UpdatedAt)));
    }

    public async Task<Result<SpaceDocView>> GetAsync(string spaceKey, long docId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var doc = await db.SpaceDocs.AsNoTracking().SingleOrDefaultAsync(d => d.Id == docId && d.ProjectId == allowed.Value!.ProjectId, ct);
        return doc is null ? AppError.NotFound("page") : await ViewAsync(doc, allowed.Value!, ct);
    }

    public async Task<Result<SpaceDocView>> CreateAsync(string spaceKey, string title, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var created = SpaceDoc.Create(allowed.Value!.ProjectId, title, allowed.Value.UserId, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.SpaceDocs.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(created.Value!, allowed.Value, ct);
    }

    public async Task<Result<SpaceDocView>> SaveAsync(string spaceKey, long docId, string title, string? document, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var doc = await db.SpaceDocs.SingleOrDefaultAsync(d => d.Id == docId && d.ProjectId == allowed.Value!.ProjectId, ct);
        if (doc is null)
        {
            return AppError.NotFound("page");
        }

        FormattedText? body = null;
        if (!string.IsNullOrWhiteSpace(document))
        {
            var parsed = FormattedText.Parse(document, "Body");
            if (!parsed.IsSuccess)
            {
                return parsed.Error!.ToAppError();
            }

            body = parsed.Value;
        }

        if (doc.Edit(title, body, allowed.Value!.UserId, time.GetUtcNow()) is { } error)
        {
            return error.ToAppError();
        }

        await db.SaveChangesAsync(ct);
        return await ViewAsync(doc, allowed.Value, ct);
    }

    public async Task<Result> DeleteAsync(string spaceKey, long docId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var doc = await db.SpaceDocs.SingleOrDefaultAsync(d => d.Id == docId && d.ProjectId == allowed.Value!.ProjectId, ct);
        if (doc is null)
        {
            return AppError.NotFound("page");
        }

        if (!CanDelete(doc, allowed.Value!))
        {
            return AppError.Forbidden("Only its author or someone who manages the space can delete this page.");
        }

        db.SpaceDocs.Remove(doc);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static bool CanDelete(SpaceDoc doc, ProjectAccessInfo caller) =>
        caller.CanManage || (caller.CanContribute && doc.CreatedById == caller.UserId);

    private async Task<SpaceDocView> ViewAsync(SpaceDoc doc, ProjectAccessInfo caller, CancellationToken ct)
    {
        var names = await users.GetAsync([doc.CreatedById, doc.UpdatedById], ct);
        return new SpaceDocView(doc.Id, doc.Title, RichText.Of(TextFormat.Document, doc.Body, doc.BodyText),
            WorkItemReads.NameOf(names, doc.CreatedById), doc.CreatedAt, WorkItemReads.NameOf(names, doc.UpdatedById), doc.UpdatedAt,
            caller.CanContribute, CanDelete(doc, caller));
    }
}
