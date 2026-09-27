using Upms.Application.Common.Results;

namespace Upms.Application.Identity;

/// <summary>First-run creation of the first administrator (FR-002). Anonymous.</summary>
public interface ISetupService
{
    Task<bool> IsSetupOpenAsync(CancellationToken ct);

    Task<Result<Guid>> CreateFirstAdministratorAsync(string setupToken, string userName, string displayName,
        string email, string password, CancellationToken ct);
}
