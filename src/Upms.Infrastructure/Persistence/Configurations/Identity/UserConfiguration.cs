using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Common;
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
        // The default gives existing accounts "aimed at me" (FR-051); a sentinel no account has makes EF always write the
        // value, so a new account's "None" is kept.
        builder.Property(u => u.EmailNotifications).HasConversion<string>().HasMaxLength(10).IsUnicode(false)
            .HasDefaultValue(EmailChoice.AimedAtMe).HasSentinel((EmailChoice)(-1));
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        // The five user types (owner's decision of 2026-10-20); the UserTypes migration gives existing accounts theirs.
        builder.Property(u => u.UserType).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(UserType.Member);
        builder.Property(u => u.RightsGranted).HasDefaultValue(Rights.None);
        builder.Property(u => u.RightsDenied).HasDefaultValue(Rights.None);
        builder.HasOne<Department>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.Ignore(u => u.IsAdministrator);
    }
}
