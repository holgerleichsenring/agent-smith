using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0328: one expectation per run (unique RunId — a replayed
/// ExpectationRatifiedEvent upserts onto the same row).
/// </summary>
public sealed class RunExpectationConfiguration : IEntityTypeConfiguration<RunExpectation>
{
    public void Configure(EntityTypeBuilder<RunExpectation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Outcome).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.RatifiedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(e => e.RunId).IsUnique();
    }
}
