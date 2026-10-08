using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-08-10b0: one row per open pull request of a swept repository.</summary>
public sealed class PrSweepStateConfiguration : IEntityTypeConfiguration<PrSweepState>
{
    public void Configure(EntityTypeBuilder<PrSweepState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrSweepStates");
        builder.HasKey(s => new { s.Repository, s.Number });
        builder.Property(s => s.Repository).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.Number).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.ReviewedHead).HasMaxLength(PersistenceLimits.Sha256Hex);
        builder.Property(s => s.CommentsSeenId).HasMaxLength(64);
    }
}
