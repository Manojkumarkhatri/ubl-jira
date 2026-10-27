using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class SprintScopeEventConfiguration : IEntityTypeConfiguration<SprintScopeEvent>
{
    public const string AppendOnlyTrigger = "TR_SprintScopeEvents_AppendOnly";

    public void Configure(EntityTypeBuilder<SprintScopeEvent> builder)
    {
        builder.ToTable("SprintScopeEvents", t => t.HasTrigger(AppendOnlyTrigger));
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
        builder.Property(e => e.Estimate).HasPrecision(4, 1);
        builder.HasOne<Sprint>().WithMany().HasForeignKey(e => e.SprintId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(e => e.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);

        // The sprint report groups a sprint's rows by kind (Phase 3 FR-029, research R11).
        builder.HasIndex(e => new { e.SprintId, e.Kind });
    }
}
