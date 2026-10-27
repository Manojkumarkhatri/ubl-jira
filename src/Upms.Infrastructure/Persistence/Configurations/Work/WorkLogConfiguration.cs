using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
{
    public void Configure(EntityTypeBuilder<WorkLog> builder)
    {
        builder.ToTable("WorkLogs", t => t.HasCheckConstraint("CK_WorkLogs_Minutes", $"[Minutes] BETWEEN 1 AND {WorkDuration.MaxMinutes}"));
        builder.Property(l => l.Note).HasMaxLength(WorkLog.NoteMaxLength);
        builder.HasIndex(l => new { l.WorkItemId, l.StartedAt });
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(l => l.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkItemVoteConfiguration : IEntityTypeConfiguration<WorkItemVote>
{
    public void Configure(EntityTypeBuilder<WorkItemVote> builder)
    {
        builder.ToTable("WorkItemVotes");
        builder.HasKey(v => new { v.WorkItemId, v.UserId });
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(v => v.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
