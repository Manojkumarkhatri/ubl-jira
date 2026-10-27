using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    /// <summary>Live, top-level stories, tasks and bugs: the cards of boards, backlogs and sprints (Phase 3 FR-013).</summary>
    public const string LiveCardsFilter = "[IsDeleted] = 0 AND [ParentId] IS NULL AND [Type] IN ('Task', 'Story', 'Bug')";

    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems", t =>
        {
            // Calendar dates in 2000–2099, the due date not before the start date (Phase 2 FR-018, research R8).
            t.HasCheckConstraint("CK_WorkItems_Dates",
                "([StartDate] IS NULL OR [StartDate] BETWEEN '2000-01-01' AND '2099-12-31') AND " +
                "([DueDate] IS NULL OR [DueDate] BETWEEN '2000-01-01' AND '2099-12-31') AND " +
                "([StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate])");

            // Estimates of 0 to 999.9 (Phase 3 FR-014); a milestone has only its date and is never a sub-task (FR-012).
            t.HasCheckConstraint("CK_WorkItems_Estimate", "[Estimate] IS NULL OR [Estimate] BETWEEN 0 AND 999.9");
            t.HasCheckConstraint("CK_WorkItems_Milestone",
                "[Type] <> 'Milestone' OR ([StartDate] IS NULL AND [Estimate] IS NULL AND [ParentId] IS NULL)");
        });
        builder.Property(w => w.Key).HasMaxLength(WorkItem.KeyMaxLength).IsUnicode(false).IsRequired();
        builder.HasIndex(w => w.Key).IsUnique();
        builder.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();
        builder.Property(w => w.Type).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
        builder.Property(w => w.Title).HasMaxLength(WorkItem.TitleMaxLength).IsRequired();
        builder.Property(w => w.Description);

        // Plain text from Phases 1–3 or a checked document, and its plain text for the List view and history (Phase 4 R4).
        builder.Property(w => w.DescriptionFormat).HasConversion<byte>();
        builder.Property(w => w.DescriptionText);

        builder.Property(w => w.Priority).HasConversion<string>().HasMaxLength(8).IsUnicode(false);
        builder.Property(w => w.StartDate).HasColumnType("date");
        builder.Property(w => w.DueDate).HasColumnType("date");
        builder.Property(w => w.Estimate).HasPrecision(4, 1);

        // An epic's color, 0–7 (Jira parity pass FR-025; research R15).
        builder.Property(w => w.Color).HasConversion<byte>();

        // Jira's time tracking and card cover.
        builder.Property(w => w.TimeSpentMinutes).HasDefaultValue(0);
        builder.Property(w => w.CoverColor).HasMaxLength(20).IsUnicode(false);

        // Fractional index compared ordinally (research R14): binary collation.
        builder.Property(w => w.Rank).HasMaxLength(Rank.MaxLength).IsUnicode(false).UseCollation("Latin1_General_BIN2").IsRequired();
        builder.Property(w => w.RowVersion).IsRowVersion();
        builder.HasQueryFilter(w => !w.IsDeleted);

        builder.HasOne<Project>().WithMany().HasForeignKey(w => w.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectStatus>().WithMany().HasForeignKey(w => w.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(w => w.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(w => w.ContainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sprint>().WithMany().HasForeignKey(w => w.SprintId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.DeletedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.AssigneeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Changes).WithOne().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(w => w.Changes).HasField("_changes").UsePropertyAccessMode(PropertyAccessMode.Field);

        // The board: the cards of a column (live, top-level stories, tasks and bugs) in rank order (research R19; Phase 3
        // research R4), covering the card fields so a 500-card board is read from the index alone (SC-002).
        builder.HasIndex(w => new { w.ProjectId, w.StatusId, w.Rank })
            .HasDatabaseName("IX_WorkItems_Board")
            .HasFilter(LiveCardsFilter)
            .IncludeProperties(w => new
            {
                w.Key,
                w.Title,
                w.Priority,
                w.ResolvedAt,
                w.RowVersion,
                w.AssigneeId,
                w.DueDate,
                w.Type,
                w.Estimate,
                w.SprintId,
            });
        builder.HasIndex(w => w.ParentId);

        // A Scrum project's cards in its one order: the backlog (open cards) and the ranker's nearest keys over the whole
        // project (Phase 3 research R8, R10).
        builder.HasIndex(w => new { w.ProjectId, w.Rank }, "IX_WorkItems_Backlog")
            .HasFilter(LiveCardsFilter)
            .IncludeProperties(w => new
            {
                w.Key,
                w.Title,
                w.Type,
                w.StatusId,
                w.AssigneeId,
                w.Estimate,
                w.ContainerId,
                w.SprintId,
                w.ResolvedAt,
                w.RowVersion,
            });

        // A sprint's cards in order, for the backlog, the sprint's start and its completion (research R10).
        builder.HasIndex(w => new { w.SprintId, w.Rank }, "IX_WorkItems_Sprint")
            .HasFilter("[IsDeleted] = 0 AND [SprintId] IS NOT NULL")
            .IncludeProperties(w => new
            {
                w.Key,
                w.Title,
                w.Type,
                w.StatusId,
                w.AssigneeId,
                w.Estimate,
                w.ContainerId,
                w.ResolvedAt,
                w.RowVersion,
            });

        // An epic's or stage's items, and its progress (Phase 3 research R7).
        builder.HasIndex(w => w.ContainerId, "IX_WorkItems_Container_Live")
            .HasFilter("[IsDeleted] = 0 AND [ContainerId] IS NOT NULL")
            .IncludeProperties(w => new { w.Type, w.StatusId, w.ResolvedAt });
        builder.HasIndex(w => new { w.ProjectId, w.ResolvedAt });

        // Counts without reading the table (SC-002): open work per column for the project list (FR-013), and the
        // done/total sub-task counts on every card (FR-017).
        builder.HasIndex(w => w.StatusId, "IX_WorkItems_Status_Live").HasFilter("[IsDeleted] = 0");
        // It also covers the sub-task cards of a board (Jira parity pass FR-027a; research R18).
        builder.HasIndex(w => new { w.ParentId, w.StatusId }, "IX_WorkItems_Subtasks_Live")
            .HasFilter("[IsDeleted] = 0 AND [ParentId] IS NOT NULL")
            .IncludeProperties(w => new
            {
                w.ProjectId,
                w.Key,
                w.Title,
                w.Priority,
                w.Rank,
                w.AssigneeId,
                w.DueDate,
                w.ResolvedAt,
                w.RowVersion,
            });

        // The List view and the timeline read a project's live items from one index (Phase 2 research R11, R12).
        builder.HasIndex(w => new { w.ProjectId, w.Number }, "IX_WorkItems_Project_Live")
            .HasFilter("[IsDeleted] = 0")
            .IncludeProperties(w => new
            {
                w.Key,
                w.Title,
                w.ParentId,
                w.StatusId,
                w.Priority,
                w.AssigneeId,
                w.StartDate,
                w.DueDate,
                w.UpdatedAt,
                w.Rank,
                w.RowVersion,
                w.Type,
                w.ContainerId,
                w.Estimate,
            });

        // "My tasks": a person's live assigned items; open ones have no ResolvedAt (Phase 2 research R13).
        builder.HasIndex(w => new { w.AssigneeId, w.ProjectId }, "IX_WorkItems_Assignee_Live")
            .HasFilter("[IsDeleted] = 0 AND [AssigneeId] IS NOT NULL")
            .IncludeProperties(w => new { w.Key, w.Title, w.ParentId, w.StatusId, w.Priority, w.DueDate, w.ResolvedAt, w.Type });

        // Deleted items keep their status, so moving a column's items and its foreign key need every row.
        builder.HasIndex(w => w.StatusId, "IX_WorkItems_StatusId");

        // The Summary page's counts and breakdowns, each one range of this index per space (Jira parity pass FR-022; research R8).
        builder.HasIndex(w => w.ProjectId, "IX_WorkItems_Summary")
            .IncludeProperties(w => new
            {
                w.Type,
                w.Priority,
                w.StatusId,
                w.AssigneeId,
                w.ContainerId,
                w.ParentId,
                w.CreatedAt,
                w.UpdatedAt,
                w.ResolvedAt,
                w.DueDate,
            })
            .HasFilter("[IsDeleted] = 0");
    }
}
