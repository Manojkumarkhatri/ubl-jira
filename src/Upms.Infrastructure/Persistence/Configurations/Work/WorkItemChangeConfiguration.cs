using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemChangeConfiguration : IEntityTypeConfiguration<WorkItemChange>
{
    public const string AppendOnlyTrigger = "TR_WorkItemChanges_AppendOnly";

    public void Configure(EntityTypeBuilder<WorkItemChange> builder)
    {
        builder.ToTable("WorkItemChanges", t => t.HasTrigger(AppendOnlyTrigger));
        builder.Property(c => c.Field).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(c => c.Note).HasMaxLength(WorkItemChange.NoteMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.WorkItemId, c.OccurredAt });

        // What a person worked on lately, and the space they last created an item in (Jira parity pass FR-002, FR-022a;
        // research R9, R10).
        builder.HasIndex(c => new { c.ActorId, c.OccurredAt }, "IX_WorkItemChanges_Actor")
            .IncludeProperties(c => new { c.WorkItemId, c.Field });

        // The Summary page's recent activity, read backwards from now (Jira parity pass FR-022; research R8).
        builder.HasIndex(c => c.OccurredAt, "IX_WorkItemChanges_OccurredAt")
            .IncludeProperties(c => new { c.WorkItemId, c.ActorId, c.Field });
    }
}
