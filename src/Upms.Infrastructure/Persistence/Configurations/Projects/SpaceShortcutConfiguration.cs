using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class SpaceShortcutConfiguration : IEntityTypeConfiguration<SpaceShortcut>
{
    public void Configure(EntityTypeBuilder<SpaceShortcut> builder)
    {
        builder.ToTable("SpaceShortcuts");
        builder.Property(s => s.Name).HasMaxLength(SpaceShortcut.NameMaxLength).IsRequired();
        builder.Property(s => s.Url).HasMaxLength(SpaceShortcut.UrlMaxLength).IsRequired();
        builder.HasIndex(s => s.ProjectId);
        builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
