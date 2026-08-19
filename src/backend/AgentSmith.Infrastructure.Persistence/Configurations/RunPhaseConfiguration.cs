using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0466: one row per phase per run. (RunId, PhaseId) is UNIQUE — a phase changes
/// standing several times (selected, then through or stopped) and every change upserts
/// the same row, so a replayed event cannot fork a phase into two.
/// </summary>
public sealed class RunPhaseConfiguration : IEntityTypeConfiguration<RunPhase>
{
    public void Configure(EntityTypeBuilder<RunPhase> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.PhaseId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.Status).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(p => new { p.RunId, p.PhaseId }).IsUnique();
    }
}
