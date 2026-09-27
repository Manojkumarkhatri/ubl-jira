using Microsoft.EntityFrameworkCore.Design;

namespace Upms.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the model without the web host (for example the CI check
/// <c>migrations has-pending-model-changes</c>). No connection is opened for model operations.</summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        AppDbContext.Create(Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=Upms;Integrated Security=false;TrustServerCertificate=True");
}
