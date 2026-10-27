using System.Reflection;
using Upms.Domain.Notifications;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Architecture.Tests;

/// <summary>Work items change only through their methods, which record history (research R17); project members only
/// through the project, which enforces the team rules (Phase 2 research R1); sprints only through their methods, which
/// keep their states, and scope events never change (Phase 3 research R10, R11); gates only through their rules (R14).</summary>
public sealed class EntityEncapsulationTests
{
    [Theory]
    [InlineData(typeof(WorkItem))]
    [InlineData(typeof(WorkItemChange))]
    [InlineData(typeof(ProjectMember))]
    [InlineData(typeof(Sprint))]
    [InlineData(typeof(SprintScopeEvent))]
    [InlineData(typeof(StageGate))]
    [InlineData(typeof(GateApprover))]
    [InlineData(typeof(GateRequest))]
    [InlineData(typeof(GateDecision))]
    [InlineData(typeof(Dependency))]
    [InlineData(typeof(Watch))]
    [InlineData(typeof(Attachment))]
    [InlineData(typeof(SavedFilter))]
    [InlineData(typeof(SavedFilterShare))]
    [InlineData(typeof(Label))]
    [InlineData(typeof(SearchIndexProgress))]
    [InlineData(typeof(WorkItemLabel))]
    [InlineData(typeof(Notification))]
    [InlineData(typeof(EmailMessage))]
    [InlineData(typeof(StarredProject))]
    [InlineData(typeof(ProjectVisit))]
    public void Entity_properties_have_no_public_setters(Type entity)
    {
        var publicSetters = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters);
    }
}
