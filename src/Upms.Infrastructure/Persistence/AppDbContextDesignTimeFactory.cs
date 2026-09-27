using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Upms.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the model without the web host (for example the CI check
/// <c>migrations has-pending-model-changes</c>, where no connection is opened). Commands that do connect, such as
/// <c>database update</c>, use the web app's development user secrets (quickstart step 2) or the
/// <c>ConnectionStrings__Default</c> environment variable, which wins.</summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>The <c>UserSecretsId</c> of Upms.Web.csproj.</summary>
    private const string WebUserSecretsId = "upms-web-7c1d2e5a-4b0f-4a8e-9d3c-2f6a1b8e0c47";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(WebUserSecretsId, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        return AppDbContext.Create(configuration.GetConnectionString("Default")
            ?? "Server=localhost,1433;Database=Upms;Integrated Security=false;TrustServerCertificate=True");
    }
}
