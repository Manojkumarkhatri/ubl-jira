using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Infrastructure.Persistence.Configurations.Projects;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.Property(p => p.Name).HasMaxLength(Portfolio.NameMaxLength).IsRequired();
        builder.Property(p => p.NormalizedName).HasMaxLength(Portfolio.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Portfolio.DescriptionMaxLength);
        builder.HasIndex(p => new { p.DepartmentId, p.NormalizedName }).IsUnique();
        builder.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PortfolioLeadConfiguration : IEntityTypeConfiguration<PortfolioLead>
{
    public void Configure(EntityTypeBuilder<PortfolioLead> builder)
    {
        builder.ToTable("PortfolioLeads");
        builder.HasKey(l => new { l.PortfolioId, l.UserId });
        builder.HasIndex(l => l.UserId);
        builder.HasOne<Portfolio>().WithMany().HasForeignKey(l => l.PortfolioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
