using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class SpaceDocConfiguration : IEntityTypeConfiguration<SpaceDoc>
{
    public void Configure(EntityTypeBuilder<SpaceDoc> builder)
    {
        builder.ToTable("SpaceDocs");
        builder.Property(d => d.Title).HasMaxLength(SpaceDoc.TitleMaxLength).IsRequired();
        builder.HasIndex(d => new { d.ProjectId, d.UpdatedAt });
        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
