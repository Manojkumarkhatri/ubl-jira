using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.Infrastructure.Persistence;
using Upms.Web.Tests.Fixtures;

namespace Upms.Web.Tests.Security;

/// <summary>Downloading and previewing attachments (Phase 4 FR-027, contracts/http-endpoints.md): access is checked on
/// every request, every refusal is the same 404, and the responses can never run as a page.</summary>
public sealed class AttachmentEndpointsTests(WebDatabaseFixture database) : IAsyncLifetime
{
    private const string Password = "a long test passphrase 4";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private UpmsWebApplicationFactory _factory = null!;
    private (long Png, long Pdf, long Checking, long Removed) _files;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new UpmsWebApplicationFactory(database.NewDatabase());
        var amina = await _factory.CreateUserAsync("amina", Password);
        await _factory.CreateUserAsync("nora", Password);
        _files = await SeedAsync(amina.Id);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>WEB with WEB-1 and four files: a checked image, a checked PDF, one still being checked and one removed.</summary>
    private async Task<(long, long, long, long)> SeedAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IAttachmentContentStore>();
        var now = DateTimeOffset.UtcNow;
        var project = Project.Create("Website", "WEB", null, ownerId, now).Value!;
        db.Projects.Add(project);
        await db.SaveChangesAsync(Ct);
        var toDo = project.Statuses[0];
        var item = WorkItem.CreateTask(project.Id, "WEB", 1, "Plan the launch", new StatusRef(toDo.Id, toDo.Name, toDo.Category), "a0",
            ChangeContext.New(ownerId, now)).Value!;
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(Ct);

        async Task<long> FileAsync(string name, byte[] bytes, Action<Attachment> state)
        {
            var attachment = Attachment.Upload(item.Id, name, bytes.Length, new byte[32], ownerId, now).Value!;
            state(attachment);
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(Ct);
            await store.WriteAsync(attachment.Id, new MemoryStream(bytes), Ct);
            return attachment.Id;
        }

        return (await FileAsync("launch.png", Png, a => a.Pass(now)),
            await FileAsync("report \"final\".pdf", Pdf, a => a.Pass(now)),
            await FileAsync("new.pdf", Pdf, _ => { }),
            await FileAsync("old.pdf", Pdf, a =>
            {
                a.Pass(now);
                a.Remove(ownerId, now);
            }));
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = _factory.CreateHttpsClient();
        using var login = await UpmsWebApplicationFactory.PostLoginAsync(client, userName, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task P4_FR027_A_checked_file_downloads_with_safe_headers()
    {
        using var client = await SignedInAsync("amina");

        using var response = await client.GetAsync(new Uri($"/attachments/{_files.Pdf}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("report \"final\".pdf", response.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("default-src 'none'; sandbox", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Resource-Policy")));
    }

    [Fact]
    public async Task An_image_previews_inline_and_other_files_have_no_preview()
    {
        using var client = await SignedInAsync("amina");

        using var preview = await client.GetAsync(new Uri($"/attachments/{_files.Png}/preview", UriKind.Relative), Ct);
        using var noPreview = await client.GetAsync(new Uri($"/attachments/{_files.Pdf}/preview", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", preview.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(Png, await preview.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal(HttpStatusCode.NotFound, noPreview.StatusCode);
    }

    [Fact]
    public async Task P4_FR027_Every_refusal_is_the_same_404()
    {
        using var amina = await SignedInAsync("amina");
        using var nora = await SignedInAsync("nora");
        var refused = new[]
        {
            await amina.GetAsync(new Uri($"/attachments/{_files.Checking}", UriKind.Relative), Ct),
            await amina.GetAsync(new Uri($"/attachments/{_files.Removed}", UriKind.Relative), Ct),
            await amina.GetAsync(new Uri("/attachments/999999", UriKind.Relative), Ct),
            await nora.GetAsync(new Uri($"/attachments/{_files.Pdf}", UriKind.Relative), Ct), // not a member of WEB
        };

        Assert.All(refused, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        var bodies = await Task.WhenAll(refused.Select(r => r.Content.ReadAsStringAsync(Ct)));
        Assert.Single(bodies.Distinct());
        Assert.All(refused, r => r.Dispose());
    }

    [Fact]
    public async Task Anonymous_requests_get_401()
    {
        using var client = _factory.CreateHttpsClient();

        using var download = await client.GetAsync(new Uri($"/attachments/{_files.Pdf}", UriKind.Relative), Ct);
        using var preview = await client.GetAsync(new Uri($"/attachments/{_files.Png}/preview", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, download.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, preview.StatusCode);
    }

    [Fact]
    public async Task A_person_gets_120_files_a_minute()
    {
        using var client = await SignedInAsync("amina");

        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i <= 120; i++)
        {
            using var response = await client.GetAsync(new Uri($"/attachments/{_files.Png}/preview", UriKind.Relative), Ct);
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
