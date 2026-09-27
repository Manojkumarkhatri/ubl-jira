using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.Property(p => p.Key).HasMaxLength(Project.KeyMaxLength).IsUnicode(false).IsRequired();
        builder.HasIndex(p => p.Key).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(p => p.NormalizedName).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.HasIndex(p => p.NormalizedName).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(Project.DescriptionMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);

        // Task creation increments NextItemNumber in this row, so a rowversion would report false
        // conflicts; details and board edits each carry their own version instead (research R16).
        builder.Property(p => p.DetailsVersion).IsConcurrencyToken();
        builder.Property(p => p.BoardVersion).IsConcurrencyToken();

        builder.HasMany(p => p.Statuses).WithOne().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Statuses).HasField("_statuses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
