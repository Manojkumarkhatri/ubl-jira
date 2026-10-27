using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work;
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

    // The company mail server, played by smtp4dev, which keeps every email and shows them through its API (Phase 4 US5).
    private readonly IContainer _mail = new ContainerBuilder("rnwood/smtp4dev:v3")
        .WithPortBinding(25, assignRandomHostPort: true)
        .WithPortBinding(80, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/api/messages")))
        .Build();

    private AppFactory? _factory;

    public string BaseUrl { get; private set; } = "";

    public IServiceProvider Services => _factory!.Services;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _mail.StartAsync());
        MailApi = new HttpClient { BaseAddress = new Uri($"http://{_mail.Hostname}:{_mail.GetMappedPublicPort(80)}/") };
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "UpmsE2E" }.ConnectionString;

        // A free port chosen first, so that links in emails (App:PublicBaseUrl) can name it.
        var port = FreePort();
        BaseUrl = $"http://localhost:{port}";
        _factory = new AppFactory(connectionString, SetupToken, BaseUrl, (_mail.Hostname, _mail.GetMappedPublicPort(25)));
        _factory.UseKestrel(port);
        _factory.StartServer();

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
            // Before the five user types anyone could create spaces, export and approve gates: a Portfolio lead's ticks.
            UserType = Upms.Domain.Common.UserType.PortfolioLead,
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

    /// <summary>Adds estimated stories at the bottom of a Scrum project's backlog directly, created by its owner (Phase 3
    /// User Story 2).</summary>
    public async Task AddStoriesAsync(string projectKey, IEnumerable<(string Title, decimal Estimate)> stories)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IWorkItemNumberAllocator>();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Key == projectKey);
        var toDo = await db.ProjectStatuses.AsNoTracking()
            .Where(s => s.ProjectId == project.Id && s.Category == StatusCategory.ToDo)
            .OrderBy(s => s.Position)
            .FirstAsync();
        // Scrum cards share one order over the whole project (Phase 3 research R8).
        var rank = await db.WorkItems.Where(w => w.ProjectId == project.Id && w.ParentId == null).MaxAsync(w => (string?)w.Rank);
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var (title, estimate) in stories)
        {
            var context = ChangeContext.New(project.OwnerId, DateTimeOffset.UtcNow);
            rank = Rank.After(rank);
            var story = WorkItem.Create(project.Id, project.Key, await numbers.NextAsync(project.Id, CancellationToken.None), WorkItemType.Story,
                title, new StatusRef(toDo.Id, toDo.Name, toDo.Category), rank, ProjectTemplate.Scrum, context).Value!;
            story.SetEstimate(estimate, ProjectTemplate.Scrum, context);
            db.WorkItems.Add(story);
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        await IndexForSearchAsync();
    }

    /// <summary>Adds items of any type directly, created by the project's owner, each with its dates and in an epic or
    /// stage (by key) or in none; a key given in <paramref name="schedule"/> is an existing item to give dates to
    /// instead (Phase 3 User Story 4).</summary>
    public async Task AddPlanAsync(string projectKey,
        IEnumerable<(WorkItemType Type, string Title, string? ContainerKey, DateOnly? Start, DateOnly? Due)> items,
        IEnumerable<(string Key, DateOnly? Start, DateOnly? Due)>? schedule = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IWorkItemNumberAllocator>();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Key == projectKey);
        var toDo = await db.ProjectStatuses.AsNoTracking()
            .Where(s => s.ProjectId == project.Id && s.Category == StatusCategory.ToDo)
            .OrderBy(s => s.Position)
            .FirstAsync();
        var rank = await db.WorkItems.Where(w => w.ProjectId == project.Id && w.ParentId == null).MaxAsync(w => (string?)w.Rank);
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var (key, start, due) in schedule ?? [])
        {
            var existing = await db.WorkItems.SingleAsync(w => w.Key == key);
            existing.Schedule(start, due, ChangeContext.New(project.OwnerId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        foreach (var (type, title, containerKey, start, due) in items)
        {
            var context = ChangeContext.New(project.OwnerId, DateTimeOffset.UtcNow);
            rank = Rank.After(rank);
            var item = WorkItem.Create(project.Id, project.Key, await numbers.NextAsync(project.Id, CancellationToken.None), type, title,
                new StatusRef(toDo.Id, toDo.Name, toDo.Category), rank, project.Template, context).Value!;
            if (containerKey is not null)
            {
                var container = await db.WorkItems.AsNoTracking().SingleAsync(w => w.Key == containerKey);
                item.PlaceIn(null, new ContainerRef(container.Id, container.ProjectId, container.Key, container.Title, container.Type), context);
            }

            if (start is not null || due is not null)
            {
                item.Schedule(start, due, context);
            }

            db.WorkItems.Add(item);
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        await IndexForSearchAsync();
    }

    /// <summary>Gives a Scrum project a sprint completed a week ago and an active sprint that started then.</summary>
    public async Task AddSprintsAsync(string projectKey)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Key == projectKey);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var completed = Sprint.Create(project.Id, project.Key, 1, ChangeContext.New(project.OwnerId, now));
        completed.Start(today.AddDays(-21), today.AddDays(-7), null, now);
        completed.Complete(now);
        db.Sprints.Add(completed);
        await db.SaveChangesAsync();
        var active = Sprint.Create(project.Id, project.Key, 2, ChangeContext.New(project.OwnerId, now));
        active.Start(today.AddDays(-7), today.AddDays(7), null, now);
        db.Sprints.Add(active);
        await db.SaveChangesAsync();
    }

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
        await IndexForSearchAsync();
    }

    /// <summary>Gives items added directly their search words now, as <c>SearchIndexWorker</c> would within 30 seconds
    /// (Phase 4 research R15).</summary>
    private async Task IndexForSearchAsync()
    {
        for (var attempt = 1; ; attempt++)
        {
            using var scope = Services.CreateScope();
            try
            {
                while (await scope.ServiceProvider.GetRequiredService<ISearchIndexBackfill>().IndexPendingAsync(CancellationToken.None) > 0)
                {
                }

                return;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // The worker indexed the same items at the same moment; what is left is picked up on the next attempt.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _sql.DisposeAsync();
        MailApi?.Dispose();
        await _mail.DisposeAsync();
    }

    /// <summary>smtp4dev's API, for the emails the app sent.</summary>
    public HttpClient MailApi { get; private set; } = null!;

    /// <summary>The plain-text bodies of the emails sent to <paramref name="address"/>, oldest first, with their subjects.</summary>
    public async Task<IReadOnlyList<(string Subject, string Text)>> EmailsToAsync(string address)
    {
        var json = await MailApi.GetFromJsonAsync<System.Text.Json.JsonElement>("api/messages");
        var items = json.ValueKind == System.Text.Json.JsonValueKind.Array ? json : json.GetProperty("results");
        var emails = new List<(string, string)>();
        foreach (var message in items.EnumerateArray().Where(m => m.GetProperty("to").ToString().Contains(address, StringComparison.OrdinalIgnoreCase)))
        {
            var text = await MailApi.GetStringAsync($"api/messages/{message.GetProperty("id")}/plaintext");
            emails.Add((message.GetProperty("subject").GetString() ?? "", text));
        }

        emails.Reverse();
        return emails;
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class AppFactory(string connectionString, string setupToken, string baseUrl, (string Host, int Port) mail)
        : WebApplicationFactory<Program>
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
            // Email through smtp4dev, with links to this app (Phase 4 US5).
            builder.UseSetting("App:PublicBaseUrl", baseUrl);
            builder.UseSetting("Email:FromAddress", "upms@e2e.example");
            builder.UseSetting("Email:Smtp:Host", mail.Host);
            builder.UseSetting("Email:Smtp:Port", mail.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Email:Smtp:Security", "None");
            // Uploads are checked by a scanner that finds only the standard antivirus test file (Phase 4 research R14).
            builder.ConfigureServices(services => services.AddSingleton<Upms.Application.Work.IMalwareScanner, TestVirusScanner>());
        }
    }
}
