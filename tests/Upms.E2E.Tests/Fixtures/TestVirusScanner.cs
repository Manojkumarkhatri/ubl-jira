using System.Text;
using Upms.Application.Work;

namespace Upms.E2E.Tests.Fixtures;

/// <summary>The browser tests' malware scanner: a file holding the standard antivirus test string is infected, anything
/// else is clean (Phase 4 User Story 2).</summary>
public sealed class TestVirusScanner : IMalwareScanner
{
    public const string TestVirus = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    public async Task<ScanOutcome> ScanAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, Encoding.Latin1, leaveOpen: true);
        return (await reader.ReadToEndAsync(ct)).Contains(TestVirus, StringComparison.Ordinal)
            ? ScanOutcome.Infected("Eicar-Test-Signature")
            : ScanOutcome.Clean;
    }
}
