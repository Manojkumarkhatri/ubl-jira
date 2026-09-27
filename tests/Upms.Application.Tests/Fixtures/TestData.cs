using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Adds a person to a project's team with a role, bypassing the member service (Phase 2).</summary>
    public async Task MemberAsync(long projectId, Guid userId, ProjectRole role = ProjectRole.Member)
    {
        await using var scope = harness.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Id == projectId);
        var added = project.AddMember(userId, role, project.OwnerId, harness.Time.GetUtcNow());
        Assert.True(added.IsSuccess, added.Error?.Message);
        await db.SaveChangesAsync();
    }

    /// <summary>Adds people to a project's team by project key, bypassing the member service (Phase 2).</summary>
    public async Task MembersAsync(string projectKey, ProjectRole role, params User[] users)
    {
        long projectId;
        await using (var scope = harness.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            projectId = await db.Projects.Where(p => p.Key == projectKey).Select(p => p.Id).SingleAsync();
        }

        foreach (var user in users)
        {
            await MemberAsync(projectId, user.Id, role);
        }
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
