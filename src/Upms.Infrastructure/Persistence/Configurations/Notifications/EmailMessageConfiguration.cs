using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;

namespace Upms.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        // The email outbox (research R10): written with its notification, deleted with it.
        builder.ToTable("EmailMessages");
        builder.Ignore(e => e.State);
        builder.Property(e => e.LastError).HasMaxLength(EmailMessage.ErrorMaxLength);
        builder.HasOne(e => e.Notification).WithMany().HasForeignKey(e => e.NotificationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecipientId).OnDelete(DeleteBehavior.Restrict);

        // What the dispatch worker claims: messages still to send, by due time.
        builder.HasIndex(e => e.NextAttemptAt, "IX_EmailMessages_Due")
            .HasFilter("[SentAt] IS NULL AND [GivenUpAt] IS NULL AND [CancelledAt] IS NULL");
    }
}
