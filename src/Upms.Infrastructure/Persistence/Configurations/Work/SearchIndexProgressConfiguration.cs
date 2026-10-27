using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class SearchIndexProgressConfiguration : IEntityTypeConfiguration<SearchIndexProgress>
{
    public void Configure(EntityTypeBuilder<SearchIndexProgress> builder)
    {
        // One row, read and moved on under a lock by the search index worker (Phase 4 research R15).
        builder.ToTable("SearchIndexProgress");
        builder.Property(p => p.Id).ValueGeneratedNever();
    }
}
