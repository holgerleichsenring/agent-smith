using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0466: one row per spec per run. (RunId, SpecId) is UNIQUE — a phase changes
/// standing several times (selected, then through or stopped) and every change upserts
/// the same row, so a replayed event cannot fork a phase into two.
/// </summary>
public sealed class RunSpecConfiguration : IEntityTypeConfiguration<RunSpec>
{
    public void Configure(EntityTypeBuilder<RunSpec> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.SpecId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.Status).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(p => new { p.RunId, p.SpecId }).IsUnique();
    }
}
