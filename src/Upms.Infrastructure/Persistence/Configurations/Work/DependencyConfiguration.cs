using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class DependencyConfiguration : IEntityTypeConfiguration<Dependency>
{
    public void Configure(EntityTypeBuilder<Dependency> builder)
    {
        builder.ToTable("Dependencies", t => t.HasCheckConstraint("CK_Dependencies_NotSelf", "[PredecessorId] <> [SuccessorId]"));
        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(d => d.PredecessorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(d => d.SuccessorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.CreatedById).OnDelete(DeleteBehavior.Restrict);

        // One link per pair; each end for the drawers (Phase 3 research R18).
        builder.HasIndex(d => new { d.PredecessorId, d.SuccessorId }).IsUnique();
        builder.HasIndex(d => d.SuccessorId);

        // A project's whole graph in one read, for the loop check and the timeline's arrows.
        builder.HasIndex(d => d.ProjectId, "IX_Dependencies_Project").IncludeProperties(d => new { d.PredecessorId, d.SuccessorId });
    }
}
