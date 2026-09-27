using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectStatusConfiguration : IEntityTypeConfiguration<ProjectStatus>
{
    public void Configure(EntityTypeBuilder<ProjectStatus> builder)
    {
        builder.ToTable("ProjectStatuses");
        builder.Property(s => s.Name).HasMaxLength(ProjectStatus.NameMaxLength).IsRequired();
        builder.Property(s => s.NormalizedName).HasMaxLength(ProjectStatus.NameMaxLength).IsRequired();
        builder.HasIndex(s => new { s.ProjectId, s.NormalizedName }).IsUnique();
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
    }
}
