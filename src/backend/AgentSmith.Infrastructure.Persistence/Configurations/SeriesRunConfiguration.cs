using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-17-0207a: UNIQUE(SeriesRunId); read by conversation and by state. The version is the concurrency
/// token two writers of one series meet on.</summary>
public sealed class SeriesRunConfiguration : IEntityTypeConfiguration<SeriesRun>
{
    public void Configure(EntityTypeBuilder<SeriesRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SeriesRuns");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SeriesRunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.State).HasMaxLength(16);
        builder.Property(s => s.Version).IsConcurrencyToken();
        builder.HasIndex(s => s.SeriesRunId).IsUnique();
        builder.HasIndex(s => s.SessionId);
        builder.HasIndex(s => s.State);
    }
}
