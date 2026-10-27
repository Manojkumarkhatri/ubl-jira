using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class WatchConfiguration : IEntityTypeConfiguration<Watch>
{
    public void Configure(EntityTypeBuilder<Watch> builder)
    {
        // One row per person and item: an item's watchers are a key range, and a person's watches one index (research R7).
        builder.ToTable("WorkItemWatchers");
        builder.HasKey(w => new { w.WorkItemId, w.UserId });
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(w => w.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(w => w.UserId);
    }
}
