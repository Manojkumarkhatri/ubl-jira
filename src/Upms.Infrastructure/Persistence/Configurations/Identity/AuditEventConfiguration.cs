using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Persistence.Configurations.Identity;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public const string AppendOnlyTrigger = "TR_AuditEvents_AppendOnly";

    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        // The trigger makes the table append-only; declaring it stops EF from using OUTPUT clauses.
        builder.ToTable("AuditEvents", t => t.HasTrigger(AppendOnlyTrigger));
        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(e => e.Target).HasMaxLength(AuditEvent.TargetMaxLength).IsRequired();
        builder.Property(e => e.Details).HasMaxLength(AuditEvent.DetailsMaxLength);
        builder.Property(e => e.SourceIp).HasMaxLength(AuditEvent.SourceIpMaxLength).IsUnicode(false);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubjectUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OccurredAt).IsDescending();
        builder.HasIndex(e => new { e.SubjectUserId, e.OccurredAt });
    }
}
