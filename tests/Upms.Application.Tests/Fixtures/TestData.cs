using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Fixtures;

/// <summary>Builders for test data (tasks.md T028).</summary>
public sealed class TestData(ServiceHarness harness)
{
    public const string DefaultPassword = "correct horse battery staple";

    public async Task<User> UserAsync(
        string userName,
        OrganizationRole role = OrganizationRole.User,
        bool isActive = true,
        bool mustChangePassword = false,
        string password = DefaultPassword)
    {
        await using var scope = harness.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = $"{char.ToUpperInvariant(userName[0])}{userName[1..]} Tester",
            OrganizationRole = role,
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            CreatedAt = harness.Time.GetUtcNow(),
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public Task<User> AdministratorAsync(string userName = "admin") => UserAsync(userName, OrganizationRole.Administrator);

    /// <summary>A bare project row with its owner (the builder switches to <c>Project.Create</c> in US1).</summary>
    public async Task<Project> ProjectAsync(string key, Guid ownerId, string? name = null)
    {
        await using var scope = harness.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var project = new Project(key, name ?? $"{key} project", null, ownerId, harness.Time.GetUtcNow());
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }
}
