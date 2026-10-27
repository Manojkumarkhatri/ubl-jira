using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        // One row per name across the organization; the normalized name makes "Launch" and "launch" one label (research R18).
        builder.ToTable("Labels");
        builder.Property(l => l.Name).HasMaxLength(Label.NameMaxLength).IsRequired();
        builder.Property(l => l.NormalizedName).HasMaxLength(Label.NameMaxLength).IsRequired();
        builder.HasIndex(l => l.NormalizedName).IsUnique();
    }
}

internal sealed class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> builder)
    {
        // An item's labels are a key range; the items with a label are one index seek (list, search and suggestions).
        builder.ToTable("WorkItemLabels");
        builder.HasKey(l => new { l.WorkItemId, l.LabelId });
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(l => l.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Label>().WithMany().HasForeignKey(l => l.LabelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.AddedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.LabelId).IncludeProperties(l => l.WorkItemId);
    }
}
