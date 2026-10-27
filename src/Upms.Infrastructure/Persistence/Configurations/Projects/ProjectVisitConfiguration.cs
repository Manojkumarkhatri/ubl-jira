using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectVisitConfiguration : IEntityTypeConfiguration<ProjectVisit>
{
    public void Configure(EntityTypeBuilder<ProjectVisit> builder)
    {
        builder.ToTable("ProjectVisits");
        builder.HasKey(v => new { v.UserId, v.ProjectId });
        // A person's spaces, latest first, for the sidebar (research R6).
        builder.HasIndex(v => new { v.UserId, v.VisitedAt }).IsDescending(false, true);
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
