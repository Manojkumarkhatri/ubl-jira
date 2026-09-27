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
    }
}
