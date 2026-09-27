using Upms.Application.Common;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
}
