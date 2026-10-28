using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class SpaceFormConfiguration : IEntityTypeConfiguration<SpaceForm>
{
    public void Configure(EntityTypeBuilder<SpaceForm> builder)
    {
        builder.ToTable("SpaceForms");
        builder.Property(f => f.Name).HasMaxLength(SpaceForm.NameMaxLength).IsRequired();
        builder.Property(f => f.Intro).HasMaxLength(SpaceForm.IntroMaxLength);
        builder.Property(f => f.WorkType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(f => f.SpaceFieldIds).HasMaxLength(SpaceForm.SpaceFieldsMaxLength).IsUnicode(false);
        builder.Ignore(f => f.SpaceFields);
        builder.HasIndex(f => f.ProjectId);
        builder.HasOne<Project>().WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
