using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class SpaceFieldConfiguration : IEntityTypeConfiguration<SpaceField>
{
    public void Configure(EntityTypeBuilder<SpaceField> builder)
    {
        builder.ToTable("SpaceFields");
        builder.Property(f => f.Name).HasMaxLength(SpaceField.NameMaxLength).IsRequired();
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(f => f.Description).HasMaxLength(SpaceField.DescriptionMaxLength);
        builder.Property(f => f.Formula).HasMaxLength(SpaceField.FormulaMaxLength);
        builder.Property(f => f.RowVersion).IsRowVersion();
        builder.Ignore(f => f.Options);
        builder.Ignore(f => f.HasOptions);

        // One live field of a name per space.
        builder.HasIndex(f => new { f.ProjectId, f.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne<Project>().WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FieldValueConfiguration : IEntityTypeConfiguration<FieldValue>
{
    public void Configure(EntityTypeBuilder<FieldValue> builder)
    {
        builder.ToTable("FieldValues");
        builder.HasKey(v => new { v.WorkItemId, v.FieldId });
        builder.Property(v => v.Value).IsRequired();
        builder.HasIndex(v => v.FieldId);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(v => v.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpaceField>().WithMany().HasForeignKey(v => v.FieldId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkTypeLayoutConfiguration : IEntityTypeConfiguration<WorkTypeLayout>
{
    public void Configure(EntityTypeBuilder<WorkTypeLayout> builder)
    {
        builder.ToTable("WorkTypeLayouts");
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(l => l.PlacementsJson).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();
        builder.Ignore(l => l.Placements);
        builder.HasIndex(l => new { l.ProjectId, l.Type }).IsUnique();
        builder.HasOne<Project>().WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
