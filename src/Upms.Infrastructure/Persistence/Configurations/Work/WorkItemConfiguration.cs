using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.Property(w => w.Key).HasMaxLength(WorkItem.KeyMaxLength).IsUnicode(false).IsRequired();
        builder.HasIndex(w => w.Key).IsUnique();
        builder.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();
        builder.Property(w => w.Type).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
        builder.Property(w => w.Title).HasMaxLength(WorkItem.TitleMaxLength).IsRequired();
        builder.Property(w => w.Description);
        builder.Property(w => w.Priority).HasConversion<string>().HasMaxLength(8).IsUnicode(false);

        // Fractional index compared ordinally (research R14): binary collation.
        builder.Property(w => w.Rank).HasMaxLength(Rank.MaxLength).IsUnicode(false).UseCollation("Latin1_General_BIN2").IsRequired();
        builder.Property(w => w.RowVersion).IsRowVersion();
        builder.HasQueryFilter(w => !w.IsDeleted);

        builder.HasOne<Project>().WithMany().HasForeignKey(w => w.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectStatus>().WithMany().HasForeignKey(w => w.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(w => w.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.DeletedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Changes).WithOne().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(w => w.Changes).HasField("_changes").UsePropertyAccessMode(PropertyAccessMode.Field);

        // The board: top-level, non-deleted items of a column in rank order (research R19), covering the card fields
        // so a 500-card board is read from the index alone (SC-002).
        builder.HasIndex(w => new { w.ProjectId, w.StatusId, w.Rank })
            .HasDatabaseName("IX_WorkItems_Board")
            .HasFilter("[IsDeleted] = 0 AND [ParentId] IS NULL")
            .IncludeProperties(w => new { w.Key, w.Title, w.Priority, w.ResolvedAt, w.RowVersion });
        builder.HasIndex(w => w.ParentId);
        builder.HasIndex(w => new { w.ProjectId, w.ResolvedAt });

        // Counts without reading the table (SC-002): open work per column for the project list (FR-013), and the
        // done/total sub-task counts on every card (FR-017).
        builder.HasIndex(w => w.StatusId, "IX_WorkItems_Status_Live").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.ParentId, w.StatusId }, "IX_WorkItems_Subtasks_Live")
            .HasFilter("[IsDeleted] = 0 AND [ParentId] IS NOT NULL");
        // Deleted items keep their status, so moving a column's items and its foreign key need every row.
        builder.HasIndex(w => w.StatusId, "IX_WorkItems_StatusId");
    }
}
