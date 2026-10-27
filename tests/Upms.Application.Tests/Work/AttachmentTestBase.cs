using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>WEB with amina's task WEB-1, a fake malware scanner, and helpers to attach, check and open files (Phase 4
/// User Story 2).</summary>
public abstract class AttachmentTestBase(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    protected static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << >> endobj\n%%EOF");

    protected FakeMalwareScanner Scanner { get; } = new();

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMalwareScanner>(Scanner);

    protected Task<Result<AttachmentView>> AttachAsync(string fileName, byte[] content, string key = "WEB-1", long? size = null) =>
        CallAsync<IAttachmentService, Result<AttachmentView>>(s => s.AddAsync(key, fileName, size ?? content.Length, new MemoryStream(content), Ct));

    protected async Task<AttachmentView> RequireAttachAsync(string fileName, byte[] content, string key = "WEB-1") =>
        (await AttachAsync(fileName, content, key)).ValueOrThrow();

    /// <summary>Runs the scan worker once, as the background service does every few seconds.</summary>
    protected Task<int> ScanAsync() => CallAsync<IAttachmentScanner, int>(s => s.ScanDueAsync(Ct));

    protected async Task<IReadOnlyList<AttachmentView>> ListAsync(string key = "WEB-1") =>
        (await CallAsync<IAttachmentService, Result<IReadOnlyList<AttachmentView>>>(s => s.ListAsync(key, Ct))).ValueOrThrow();

    protected Task<Result<AttachmentFile>> OpenAsync(long id, bool preview = false) =>
        CallAsync<IAttachmentService, Result<AttachmentFile>>(s => s.OpenAsync(id, preview, Ct));

    /// <summary>The bytes the database holds for an attachment, or null when there are none.</summary>
    protected Task<byte[]?> StoredBytesAsync(long id) =>
        QueryAsync(db => db.Database
            .SqlQuery<byte[]>($"SELECT Content AS Value FROM AttachmentContents WHERE AttachmentId = {id}")
            .SingleOrDefaultAsync(Ct));

    protected static async Task<byte[]> ReadAllAsync(AttachmentFile file)
    {
        await using var content = file.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, Ct);
        return copy.ToArray();
    }
}
