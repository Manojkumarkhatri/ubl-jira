using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Common;
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
        builder.Property(p => p.NavigationViews).HasMaxLength(Project.NavigationViewsMaxLength).IsUnicode(false);

        // The TemplatesAndTypes migration makes every existing project Kanban (Phase 3 FR-004).
        builder.Property(p => p.Template).HasConversion<string>().HasMaxLength(10).IsUnicode(false);

        // Existing spaces get all four work types (Jira parity pass FR-017; research R14). The sentinel makes EF always
        // send the value, so the database default applies only to rows written before the column existed.
        builder.Property(p => p.WorkTypes).HasDefaultValue(WorkTypeSet.All).HasSentinel((WorkTypeSet)(-1));
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);

        // A space in the trash is hidden from every query; the trash and restore read it with IgnoreQueryFilters. Its key
        // and name stay taken, so restoring never clashes.
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.DeletedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Portfolio>().WithMany().HasForeignKey(p => p.PortfolioId).OnDelete(DeleteBehavior.SetNull);

        // Task creation increments NextItemNumber in this row, so a rowversion would report false
        // conflicts; details and board edits each carry their own version instead (research R16).
        builder.Property(p => p.DetailsVersion).IsConcurrencyToken();
        builder.Property(p => p.BoardVersion).IsConcurrencyToken();
        builder.Property(p => p.MembersVersion).IsConcurrencyToken();

        builder.HasMany(p => p.Statuses).WithOne().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Statuses).HasField("_statuses").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Members).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
