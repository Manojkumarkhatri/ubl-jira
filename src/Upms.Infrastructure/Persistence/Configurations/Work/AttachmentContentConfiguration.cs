using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class AttachmentContentConfiguration : IEntityTypeConfiguration<AttachmentContent>
{
    public void Configure(EntityTypeBuilder<AttachmentContent> builder)
    {
        // In their own table, so item and attachment queries never touch the bytes (research R12); deleted only by a purge.
        builder.ToTable("AttachmentContents");
        builder.HasKey(c => c.AttachmentId);
        builder.Property(c => c.AttachmentId).ValueGeneratedNever();
        builder.Property(c => c.Content).IsRequired();
        builder.HasOne<Attachment>().WithOne().HasForeignKey<AttachmentContent>(c => c.AttachmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
