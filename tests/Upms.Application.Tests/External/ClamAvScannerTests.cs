using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Infrastructure;

namespace Upms.Application.Tests.External;

/// <summary>One ClamAV daemon for the class: its first start downloads the virus definitions.</summary>
public sealed class ClamAvFixture : IAsyncLifetime
{
    public IContainer Container { get; } = new ContainerBuilder("clamav/clamav:stable")
        .WithPortBinding(3310, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("socket found, clamd started"))
        .Build();

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await Container.StartAsync(timeout.Token);
    }

    public async ValueTask DisposeAsync() => await Container.DisposeAsync();
}

/// <summary>The ClamAV client against a real daemon (Phase 4 research R14, R27): the standard antivirus test file is found,
/// a clean file passes, and a daemon that is not there is "unavailable". It pulls the ClamAV image and waits for its virus
/// definitions, so it runs in its own CI job.</summary>
[Trait("Category", "ExternalServices")]
public sealed class ClamAvScannerTests(ClamAvFixture clamav) : IClassFixture<ClamAvFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The scanner as the app registers it, pointed at <paramref name="port"/>.</summary>
    private static IMalwareScanner Scanner(string host, int port)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=unused",
            ["Malware:ClamAv:Host"] = host,
            ["Malware:ClamAv:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Malware:ClamAv:Timeout"] = "00:00:30",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUpmsInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<IMalwareScanner>();
    }

    [Fact]
    public async Task The_standard_antivirus_test_file_is_found()
    {
        var outcome = await Scanner(clamav.Container.Hostname, clamav.Container.GetMappedPublicPort(3310))
            .ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes(FakeMalwareScanner.TestVirus)), Ct);

        Assert.Equal(ScanVerdict.Infected, outcome.Verdict);
        Assert.Contains("Eicar", outcome.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_clean_file_passes_even_across_several_chunks()
    {
        var text = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("A plain line of a log file.\n", 10_000)));

        var outcome = await Scanner(clamav.Container.Hostname, clamav.Container.GetMappedPublicPort(3310)).ScanAsync(new MemoryStream(text), Ct);

        Assert.Equal(ScanVerdict.Clean, outcome.Verdict);
    }

    [Fact]
    public async Task A_daemon_that_is_not_there_is_unavailable()
    {
        var outcome = await Scanner("127.0.0.1", 1).ScanAsync(new MemoryStream("x"u8.ToArray()), Ct);

        Assert.Equal(ScanVerdict.Unavailable, outcome.Verdict);
    }
}
