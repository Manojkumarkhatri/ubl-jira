using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.Property(c => c.Body).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.WorkItemId, c.CreatedAt });
    }
}
