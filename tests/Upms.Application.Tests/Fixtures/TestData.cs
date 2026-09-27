using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

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

    /// <summary>A project with its default columns, created through the domain factory.</summary>
    public async Task<Project> ProjectAsync(string key, Guid ownerId, string? name = null)
    {
        await using var scope = harness.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var project = Project.Create(name ?? $"{key} project", key, null, ownerId, harness.Time.GetUtcNow()).Value!;
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    /// <summary>A task with a chosen rank, bypassing the board service (for rank tests).</summary>
    public async Task<WorkItem> WorkItemAsync(long projectId, string projectKey, StatusRef status, string title, string rank)
    {
        await using var scope = harness.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var allocator = scope.ServiceProvider.GetRequiredService<IWorkItemNumberAllocator>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var number = await allocator.NextAsync(projectId, CancellationToken.None);
        var item = WorkItem.CreateTask(projectId, projectKey, number, title, status, rank,
            ChangeContext.New(harness.CurrentUser.UserId ?? Guid.Empty, harness.Time.GetUtcNow())).Value!;
        db.WorkItems.Add(item);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return item;
    }
}
