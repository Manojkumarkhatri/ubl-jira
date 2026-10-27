using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        // Sprint dates follow the work item date rules: 2000–2099, the end not before the start (Phase 3 FR-025).
        builder.ToTable("Sprints", t => t.HasCheckConstraint("CK_Sprints_Dates",
            "[StartDate] IS NULL OR [EndDate] IS NULL OR [EndDate] >= [StartDate]"));
        builder.Property(s => s.Name).HasMaxLength(Sprint.NameMaxLength).IsRequired();
        builder.Property(s => s.Goal).HasMaxLength(Sprint.GoalMaxLength);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(s => s.StartDate).HasColumnType("date");
        builder.Property(s => s.EndDate).HasColumnType("date");
        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.Ignore(s => s.Ref);

        builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ProjectId, s.Number }).IsUnique();

        // At most one active sprint per project, in the database as well as under the planning lock (research R9).
        builder.HasIndex(s => s.ProjectId, "IX_Sprints_OneActive").IsUnique().HasFilter("[State] = 'Active'");
    }
}
