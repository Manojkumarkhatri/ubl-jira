using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence.Configurations.Work;

internal sealed class StageGateConfiguration : IEntityTypeConfiguration<StageGate>
{
    public void Configure(EntityTypeBuilder<StageGate> builder)
    {
        builder.ToTable("StageGates");
        builder.Property(g => g.State).HasConversion<string>().HasMaxLength(18).IsUnicode(false);
        builder.Property(g => g.RowVersion).IsRowVersion();
        builder.HasOne<Project>().WithMany().HasForeignKey(g => g.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // One gate per stage (Phase 3 FR-031). The stage may be soft-deleted, so its gate stays.
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(g => g.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => g.StageId).IsUnique();

        builder.HasMany(g => g.Approvers).WithOne().HasForeignKey(a => a.GateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Approvers).HasField("_approvers").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(g => g.Requests).WithOne().HasForeignKey(r => r.GateId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(g => g.Requests).HasField("_requests").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(g => g.CurrentRequest);
        builder.Ignore(g => g.LatestRequest);
        builder.Ignore(g => g.SentBackNote);
        builder.Ignore(g => g.IsApproved);
        builder.Ignore(g => g.ApprovalsNeeded);
    }
}

internal sealed class GateApproverConfiguration : IEntityTypeConfiguration<GateApprover>
{
    public void Configure(EntityTypeBuilder<GateApprover> builder)
    {
        builder.ToTable("GateApprovers");
        builder.HasKey(a => new { a.GateId, a.UserId });
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.AddedById).OnDelete(DeleteBehavior.Restrict);

        // "Gates waiting for you" starts from the person (Phase 3 FR-041).
        builder.HasIndex(a => a.UserId);
    }
}

internal sealed class GateRequestConfiguration : IEntityTypeConfiguration<GateRequest>
{
    public void Configure(EntityTypeBuilder<GateRequest> builder)
    {
        builder.ToTable("GateRequests");
        builder.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.HasIndex(r => new { r.GateId, r.Number }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ClosedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Decisions).WithOne().HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(r => r.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);

        // The pending requests, for "Gates waiting for you" (research R17).
        builder.HasIndex(r => r.GateId, "IX_GateRequests_Pending")
            .HasFilter("[Outcome] = 'Pending'")
            .IncludeProperties(r => new { r.Number, r.RequestedById, r.RequestedAt });
    }
}

internal sealed class GateDecisionConfiguration : IEntityTypeConfiguration<GateDecision>
{
    public const string AppendOnlyTrigger = "TR_GateDecisions_AppendOnly";

    public void Configure(EntityTypeBuilder<GateDecision> builder)
    {
        builder.ToTable("GateDecisions", t => t.HasTrigger(AppendOnlyTrigger));
        builder.Property(d => d.Decision).HasConversion<string>().HasMaxLength(8).IsUnicode(false);
        builder.Property(d => d.Note).HasMaxLength(StageGate.NoteMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.ApproverId).OnDelete(DeleteBehavior.Restrict);

        // One decision per approver and request (Phase 3 FR-037).
        builder.HasIndex(d => new { d.RequestId, d.ApproverId }).IsUnique();
    }
}
