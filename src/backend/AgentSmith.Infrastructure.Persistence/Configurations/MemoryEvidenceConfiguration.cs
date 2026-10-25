using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-25-9324a: the memory ledger. Read by (RepoKey, Slug) for a repository's standing and by (RunId, SpecId)
/// when a spec's reliance or outcome is rewritten; no foreign key to the run, which it outlives.
/// </summary>
public sealed class MemoryEvidenceConfiguration : IEntityTypeConfiguration<MemoryEvidence>
{
    private const int Short = 32;

    public void Configure(EntityTypeBuilder<MemoryEvidence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("MemoryEvidence");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RepoKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Slug).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Kind).HasMaxLength(Short);
        builder.Property(e => e.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.SpecId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.CriterionKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(e => new { e.RepoKey, e.Slug });
        builder.HasIndex(e => new { e.RunId, e.SpecId });
    }
}
