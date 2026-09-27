using Upms.Application.Common;

namespace Upms.Application.Tests.Fixtures;

/// <summary>The caller for service calls; tests switch users by setting <see cref="UserId"/>.</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
}
