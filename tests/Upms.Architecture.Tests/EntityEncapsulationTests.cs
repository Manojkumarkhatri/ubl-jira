using System.Reflection;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Architecture.Tests;

/// <summary>Work items change only through their methods, which record history (research R17); project members only
/// through the project, which enforces the team rules (Phase 2 research R1).</summary>
public sealed class EntityEncapsulationTests
{
    [Theory]
    [InlineData(typeof(WorkItem))]
    [InlineData(typeof(WorkItemChange))]
    [InlineData(typeof(ProjectMember))]
    public void Entity_properties_have_no_public_setters(Type entity)
    {
        var publicSetters = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters);
    }
}
