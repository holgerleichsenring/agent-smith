using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0336: one capacity-ledger row per run (unique RunId — a re-record upserts).
/// The reserved set is summed on the budget check; FootprintJson is an unbounded
/// operator-facing blob (not indexed, not length-capped).
/// </summary>
public sealed class RunCapacityConfiguration : IEntityTypeConfiguration<RunCapacity>
{
    public void Configure(EntityTypeBuilder<RunCapacity> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(c => c.RunId).IsUnique();
        builder.HasIndex(c => c.Reserved);
    }
}
