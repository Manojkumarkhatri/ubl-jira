using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(n => n.Detail).HasMaxLength(Notification.DetailMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(n => n.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(n => n.WorkItemId).OnDelete(DeleteBehavior.Restrict);

        // The bell's unread count and the panel, newest first, with the project for the visibility check (research R9).
        builder.HasIndex(n => new { n.RecipientId, n.ReadAt, n.OccurredAt }, "IX_Notifications_Recipient")
            .IsDescending(false, false, true)
            .IncludeProperties(n => n.ProjectId);

        // The daily clean-up of notifications older than 90 days (FR-021).
        builder.HasIndex(n => n.OccurredAt);
    }
}
