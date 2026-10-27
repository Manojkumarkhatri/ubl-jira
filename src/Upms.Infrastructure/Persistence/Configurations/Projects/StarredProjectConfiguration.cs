using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class StarredProjectConfiguration : IEntityTypeConfiguration<StarredProject>
{
    public void Configure(EntityTypeBuilder<StarredProject> builder)
    {
        builder.ToTable("StarredProjects");
        builder.HasKey(s => new { s.UserId, s.ProjectId });
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
