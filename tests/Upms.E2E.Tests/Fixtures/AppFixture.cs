using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.E2E.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(AppFixture))]
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Upms.E2E.Tests.Fixtures;

/// <summary>The real app on Kestrel with its own SQL Server, set up with a first administrator
/// (tasks.md T050). Browser tests talk to <see cref="BaseUrl"/>.</summary>
public sealed class AppFixture : IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "first run passphrase 2026";
    private const string SetupToken = "e2e-setup-token";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private AppFactory? _factory;

    public string BaseUrl { get; private set; } = "";

    public IServiceProvider Services => _factory!.Services;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "UpmsE2E" }.ConnectionString;
        _factory = new AppFactory(connectionString, SetupToken);
        _factory.UseKestrel(0);
        _factory.StartServer();
        var addresses = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server did not report its address.");
        var port = new Uri(addresses.Addresses.First().Replace("[::]", "localhost", StringComparison.Ordinal)).Port;
        BaseUrl = $"http://localhost:{port}";

        using var scope = _factory.Services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        var result = await setup.CreateFirstAdministratorAsync(SetupToken, AdminUserName, "Ada Admin", "admin@example.com",
            AdminPassword, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }

    /// <summary>Creates a user directly (as an administrator would) and returns the password to use.</summary>
    public async Task<string> CreateUserAsync(string userName, string displayName, bool mustChangePassword = false)
    {
        var password = $"{new string(userName.Reverse().ToArray())} temporary pass 42";
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var result = await userManager.CreateAsync(new User
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = displayName,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTimeOffset.UtcNow,
        }, password);
        return result.Succeeded
            ? password
            : throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>Adds an existing user to a project's team directly (Phase 2: projects are members-only).</summary>
    public async Task AddMemberAsync(string projectKey, string userName, ProjectRole role = ProjectRole.Member)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var userId = await db.Users.Where(u => u.UserName == userName).Select(u => u.Id).SingleAsync();
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Key == projectKey);
        var added = project.AddMember(userId, role, project.OwnerId, DateTimeOffset.UtcNow);
        if (!added.IsSuccess)
        {
            throw new InvalidOperationException(added.Error!.Message);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Adds tasks at the end of a project's first "to do" column directly, created by its owner, for lists too
    /// long to type; each may have a due date and an assignee (by user name).</summary>
    public Task AddTasksAsync(string projectKey, IEnumerable<(string Title, DateOnly? Due, string? Assignee)> tasks) =>
        AddAsync(projectKey, tasks.Select(t => (t.Title, (DateOnly?)null, t.Due, t.Assignee)));

    /// <summary>Adds tasks with start and due dates directly, as <see cref="AddTasksAsync"/> does.</summary>
    public Task AddScheduledTasksAsync(string projectKey, IEnumerable<(string Title, DateOnly? Start, DateOnly? Due)> tasks) =>
        AddAsync(projectKey, tasks.Select(t => (t.Title, t.Start, t.Due, (string?)null)));

    private async Task AddAsync(string projectKey, IEnumerable<(string Title, DateOnly? Start, DateOnly? Due, string? Assignee)> tasks)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IWorkItemNumberAllocator>();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Key == projectKey);
        var toDo = await db.ProjectStatuses.AsNoTracking()
            .Where(s => s.ProjectId == project.Id && s.Category == StatusCategory.ToDo)
            .OrderBy(s => s.Position)
            .FirstAsync();
        var rank = await db.WorkItems.Where(w => w.StatusId == toDo.Id && w.ParentId == null).MaxAsync(w => (string?)w.Rank);
        var people = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.UserName!, u => new PersonRef(u.Id, u.DisplayName));
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var (title, start, due, assignee) in tasks)
        {
            var context = ChangeContext.New(project.OwnerId, DateTimeOffset.UtcNow);
            rank = Rank.After(rank);
            var item = WorkItem.CreateTask(project.Id, project.Key, await numbers.NextAsync(project.Id, CancellationToken.None), title,
                new StatusRef(toDo.Id, toDo.Name, toDo.Category), rank, context).Value!;
            if (start is not null || due is not null)
            {
                item.Schedule(start, due, context);
            }

            if (assignee is not null)
            {
                item.Assign(null, people[assignee], context);
            }

            db.WorkItems.Add(item);
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _sql.DisposeAsync();
    }

    private sealed class AppFactory(string connectionString, string setupToken) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets(); // outside Development, serve _framework and wwwroot from the build output
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Setup:Token", setupToken);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            // Every test signs in from 127.0.0.1; the real limit (10 per minute) is covered by HostSecurityTests.
            builder.UseSetting("RateLimiting:SignInPerMinute", "1000");
        }
    }
}
