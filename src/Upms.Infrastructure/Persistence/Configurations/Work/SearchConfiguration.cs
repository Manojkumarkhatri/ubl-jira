using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class SearchTermConfiguration : IEntityTypeConfiguration<SearchTerm>
{
    public void Configure(EntityTypeBuilder<SearchTerm> builder)
    {
        // An item's words are a key range, rewritten with each change; a typed word is a prefix seek on Term, carrying the
        // project for the access filter (Phase 4 research R15, R26).
        builder.ToTable("SearchTerms");
        builder.HasKey(t => new { t.WorkItemId, t.Term });
        builder.Property(t => t.Term).HasMaxLength(SearchWords.MaxLength).UseCollation("Latin1_General_100_BIN2");
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(t => t.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.Term, "IX_SearchTerms_Term").IncludeProperties(t => new { t.ProjectId, t.WorkItemId });
    }
}

internal sealed class RecentViewConfiguration : IEntityTypeConfiguration<RecentView>
{
    public void Configure(EntityTypeBuilder<RecentView> builder)
    {
        builder.ToTable("RecentViews");
        builder.HasKey(r => new { r.UserId, r.WorkItemId });
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(r => r.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.UserId, r.ViewedAt }, "IX_RecentViews_Latest").IsDescending(false, true);
    }
}

internal sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("SavedFilters");
        builder.Property(f => f.Name).HasMaxLength(SavedFilter.NameMaxLength).IsRequired();
        builder.Property(f => f.NormalizedName).HasMaxLength(SavedFilter.NameMaxLength).IsRequired();
        builder.Property(f => f.Query).HasMaxLength(SavedFilter.QueryMaxLength).IsRequired();
        builder.Property(f => f.RowVersion).IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.OwnerId, f.NormalizedName }).IsUnique(); // FR-039
        builder.HasMany(f => f.Shares).WithOne().HasForeignKey(s => s.SavedFilterId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Shares).HasField("_shares").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SavedFilterShareConfiguration : IEntityTypeConfiguration<SavedFilterShare>
{
    public void Configure(EntityTypeBuilder<SavedFilterShare> builder)
    {
        builder.ToTable("SavedFilterShares");
        builder.HasKey(s => new { s.SavedFilterId, s.ProjectId });
        builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.ProjectId);
    }
}
