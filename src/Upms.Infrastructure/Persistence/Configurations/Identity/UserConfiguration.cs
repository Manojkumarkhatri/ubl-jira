using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.UserName).HasMaxLength(User.UserNameMaxLength);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(User.UserNameMaxLength);
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.TimeZoneId).HasMaxLength(User.TimeZoneIdMaxLength).IsUnicode(false);
        builder.Property(u => u.OrganizationRole).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Ignore(u => u.IsAdministrator);
    }
}
