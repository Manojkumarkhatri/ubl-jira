using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Persistence.Configurations.Identity;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.NormalizedName).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.HasIndex(d => d.NormalizedName).IsUnique();
    }
}

internal sealed class UserTypeRightsConfiguration : IEntityTypeConfiguration<UserTypeRights>
{
    public void Configure(EntityTypeBuilder<UserTypeRights> builder)
    {
        builder.ToTable("UserTypeRights");
        builder.HasKey(r => r.Type);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20).IsUnicode(false).ValueGeneratedNever();
        builder.HasData(UserTypes.Ordered.Select(type => new UserTypeRights(type, RightsCatalog.Defaults(type))));
    }
}
