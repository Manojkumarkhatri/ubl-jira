using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upms.Application.Notifications;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Tests.Notifications;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Notifications;
using Upms.Infrastructure.Email;

namespace Upms.Application.Tests.External;

/// <summary>One smtp4dev mail server for the class: it accepts every email and shows them through its API.</summary>
public sealed class Smtp4devFixture : IAsyncLifetime
{
    public IContainer Container { get; } = new ContainerBuilder("rnwood/smtp4dev:v3")
        .WithPortBinding(25, assignRandomHostPort: true)
        .WithPortBinding(80, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/api/messages")))
        .Build();

    public HttpClient Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await Container.StartAsync(timeout.Token);
        Api = new HttpClient { BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(80)}/") };
    }

    public async ValueTask DisposeAsync()
    {
        Api.Dispose();
        await Container.DisposeAsync();
    }

    /// <summary>The emails received, newest first, as (id, subject, recipients, sender).</summary>
    public async Task<IReadOnlyList<(string Id, string Subject, string To, string From)>> MessagesAsync(CancellationToken ct)
    {
        var json = await Api.GetFromJsonAsync<JsonElement>("api/messages", ct);
        var items = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("results");
        return items.EnumerateArray()
            .Select(m => (m.GetProperty("id").ToString(), m.GetProperty("subject").GetString() ?? "", m.GetProperty("to").ToString(),
                m.GetProperty("from").ToString()))
            .ToList();
    }
}

/// <summary>Email through a real SMTP server (Phase 4 FR-049, FR-052, research R10, R27): the email arrives with the right
/// sender, subject and link, and one sent while the server cannot be reached is retried and delivered later. It pulls the
/// smtp4dev image, so it runs in its own CI job.</summary>
[Trait("Category", "ExternalServices")]
public sealed class SmtpDeliveryTests(SqlServerFixture fixture, Smtp4devFixture smtp) : EmailTestBase(fixture), IClassFixture<Smtp4devFixture>
{
    private readonly SmtpOptions _smtp = new() { Host = smtp.Container.Hostname, Port = smtp.Container.GetMappedPublicPort(25), Security = SecureSocketOptions.None };

    protected override void ConfigureServices(IServiceCollection services)
    {
        // The real transport, pointed at smtp4dev through settings the test can change.
        services.AddSingleton<IOptions<SmtpOptions>>(Options.Create(_smtp));
        services.Configure<EmailOptions>(o =>
        {
            o.FromAddress = "upms@bank.example";
            o.PublicBaseUrl = BaseUrl;
        });
    }

    [Fact]
    public async Task P4_US5_AS1_An_assignment_arrives_with_sender_subject_and_link()
    {
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(Bilal.Id))).IsSuccess);

        Assert.Equal(1, await DispatchAsync());

        var message = Assert.Single(await smtp.MessagesAsync(Ct), m => m.To.Contains("bilal@example.com", StringComparison.Ordinal));
        Assert.Equal("[WEB-1] Plan the launch", message.Subject);
        var text = await smtp.Api.GetStringAsync($"api/messages/{message.Id}/plaintext", Ct);
        Assert.Contains("Amina Tester assigned WEB-1 Plan the launch to you.", text, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/spaces/WEB/board?task=WEB-1", text, StringComparison.Ordinal);
        Assert.Contains("upms@bank.example", message.From, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P4_FR052_An_email_sent_while_the_server_is_unreachable_is_delivered_later()
    {
        var port = _smtp.Port;
        _smtp.Port = 1; // nothing listens there
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(Omar.Id))).IsSuccess);

        Assert.Equal(1, await DispatchAsync());
        Assert.Equal([(Omar.Id, NotificationKind.Assigned, EmailState.Pending)], await EmailsAsync());

        _smtp.Port = port;
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await DispatchAsync());

        Assert.Equal([(Omar.Id, NotificationKind.Assigned, EmailState.Sent)], await EmailsAsync());
        Assert.Contains(await smtp.MessagesAsync(Ct), m => m.To.Contains("omar@example.com", StringComparison.Ordinal));
    }
}
