using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Upms.Application;
using Upms.Infrastructure;
using Upms.Infrastructure.Persistence;
using Upms.Web.Components;
using Upms.Web.Endpoints;
using Upms.Web.Observability;
using Upms.Web.Security;

// Composition root (research R4): the only place in Upms.Web that uses Upms.Infrastructure.
var builder = WebApplication.CreateBuilder(args);

builder.AddUpmsObservability();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddUpmsApplication(builder.Configuration);
builder.Services.AddUpmsInfrastructure(builder.Configuration);
builder.Services.AddUpmsWeb();

// Data protection keys live in the database (research R25), protected by a certificate when configured.
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("U-PMS")
    .PersistKeysToDbContext<AppDbContext>();
if (builder.Configuration["DataProtection:CertificatePath"] is { Length: > 0 } certificatePath)
{
    dataProtection.ProtectKeysWithCertificate(
        X509CertificateLoader.LoadPkcs12FromFile(certificatePath, builder.Configuration["DataProtection:CertificatePassword"]));
}

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    // Behind a TLS-terminating reverse proxy (see docs/operations/deployment.md).
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateUpmsDatabaseAsync();
}

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseUpmsSecurityHeaders();
app.UseUpmsStatusCodePages();
app.UseHttpsRedirection();
app.UseBlazorOriginCheck();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMustChangePassword();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAccountEndpoints();
app.MapKeepAlive();
app.MapHealthEndpoints();

await app.RunAsync();

/// <summary>Entry point, public for WebApplicationFactory-based tests.</summary>
public partial class Program;
