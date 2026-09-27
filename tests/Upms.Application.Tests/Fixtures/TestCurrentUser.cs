using Upms.Application.Common;

namespace Upms.Application.Tests.Fixtures;

/// <summary>The caller for service calls; tests switch users by setting <see cref="UserId"/>, or give one async
/// flow its own caller with <see cref="UseInThisFlow"/> so that parallel calls can come from different users.</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    private readonly AsyncLocal<Guid?> _flowUserId = new();
    private Guid? _userId;

    public Guid? UserId
    {
        get => _flowUserId.Value ?? _userId;
        set => _userId = value;
    }

    /// <summary>Makes <paramref name="userId"/> the caller for the rest of the current async flow only.</summary>
    public void UseInThisFlow(Guid userId) => _flowUserId.Value = userId;
}
