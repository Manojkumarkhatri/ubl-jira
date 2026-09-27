using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
        builder.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();

        // The project list and "My tasks" look memberships up by person (Phase 2 research R4).
        builder.HasIndex(m => m.UserId).IncludeProperties(m => m.Role);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.AddedById).OnDelete(DeleteBehavior.Restrict);
    }
}
