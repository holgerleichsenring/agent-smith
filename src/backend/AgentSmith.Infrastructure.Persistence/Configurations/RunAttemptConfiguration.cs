using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-13-9a59d: one attempt row per run — RunId is UNIQUE, so two replicas inserting the same
/// run's row resolve through the index and a replayed event updates the row it already wrote.
/// </summary>
public sealed class RunAttemptConfiguration : IEntityTypeConfiguration<RunAttempt>
{
    private const int Short = 64;

    public void Configure(EntityTypeBuilder<RunAttempt> builder)
    {
        builder.ToTable("RunAttempts");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.RunId).IsUnique();
        builder.Property(a => a.CauseChannel).HasMaxLength(Short);
        builder.Property(a => a.SourceKind).HasMaxLength(Short);
        builder.Property(a => a.TicketFingerprint).HasMaxLength(PersistenceLimits.Sha256Hex);
        builder.Property(a => a.SeriesId).HasMaxLength(PersistenceLimits.IndexedString);
    }
}
