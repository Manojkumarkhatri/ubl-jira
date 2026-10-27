using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", t => t.HasCheckConstraint("CK_Attachments_Size", "[Size] BETWEEN 1 AND 26214400"));
        builder.Property(a => a.FileName).HasMaxLength(AttachmentRules.FileNameMaxLength).IsRequired();
        builder.Property(a => a.Extension).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(a => a.Sha256).HasMaxLength(32).IsFixedLength();
        builder.Property(a => a.CheckStatus).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(a => a.ThreatName).HasMaxLength(200);
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.Ignore(a => a.IsImage);
        builder.Ignore(a => a.IsAvailable);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(a => a.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.RemovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.PurgedById).OnDelete(DeleteBehavior.Restrict);

        // An item's listed files for the drawer, and the files waiting for the scan worker (research R14).
        builder.HasIndex(a => a.WorkItemId, "IX_Attachments_Item_Listed").HasFilter("[RemovedAt] IS NULL AND [PurgedAt] IS NULL");
        builder.HasIndex(a => a.NextCheckAt, "IX_Attachments_Checking").HasFilter("[CheckStatus] = 'Checking'");
    }
}
