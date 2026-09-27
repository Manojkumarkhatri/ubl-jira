using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Persistence.Configurations.Identity;

internal sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("OrganizationSettings");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.DefaultTimeZoneId).HasMaxLength(User.TimeZoneIdMaxLength).IsUnicode(false);
        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.HasData(new
        {
            Id = OrganizationSettings.SingletonId,
            DefaultTimeZoneId = OrganizationSettings.DefaultTimeZone,
            IdleTimeoutMinutes = OrganizationSettings.DefaultIdleTimeoutMinutes,
        });
    }
}
