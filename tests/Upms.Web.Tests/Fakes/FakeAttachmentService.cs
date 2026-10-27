using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeAttachmentService : IAttachmentService
{
    private long _nextId = 50;

    public List<AttachmentView> Items { get; } = [];

    public List<(string Key, string FileName, long Size, byte[] Bytes)> Added { get; } = [];

    public List<long> Removed { get; } = [];

    public List<long> Purged { get; } = [];

    public int Lists { get; private set; }

    public Func<string, AppError?>? Refuse { get; set; }

    public Task<Result<SpaceAttachments>> ListForSpaceAsync(string spaceKey, CancellationToken ct) =>
        Task.FromResult(Result<SpaceAttachments>.Ok(new SpaceAttachments(spaceKey, spaceKey, Upms.Domain.Common.ProjectTemplate.Kanban, false, true, false, [])));

    public Task<Result<IReadOnlyList<AttachmentView>>> ListAsync(string workItemKey, CancellationToken ct)
    {
        Lists++;
        return Task.FromResult(Result<IReadOnlyList<AttachmentView>>.Ok(Items.ToList()));
    }

    public async Task<Result<AttachmentView>> AddAsync(string workItemKey, string fileName, long size, Stream content, CancellationToken ct)
    {
        if (Refuse?.Invoke(fileName) is { } error)
        {
            return error;
        }

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Added.Add((workItemKey, fileName, size, copy.ToArray()));
        var view = new AttachmentView(_nextId++, fileName, size, "Amina Khan", DateTimeOffset.UtcNow, AttachmentCheckStatus.Checking,
            AttachmentRules.IsImage(AttachmentRules.ExtensionOf(fileName)), CanRemove: true);
        Items.Add(view);
        return view;
    }

    public Task<Result> RemoveAsync(long attachmentId, CancellationToken ct)
    {
        Removed.Add(attachmentId);
        Items.RemoveAll(a => a.Id == attachmentId);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> PurgeAsync(long attachmentId, CancellationToken ct)
    {
        Purged.Add(attachmentId);
        Items.RemoveAll(a => a.Id == attachmentId);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<AttachmentFile>> OpenAsync(long attachmentId, bool preview, CancellationToken ct) =>
        Task.FromResult(Result<AttachmentFile>.Fail(AppError.NotFound("file")));
}
