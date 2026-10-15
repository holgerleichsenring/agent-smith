using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-13-9a59b: UNIQUE(PrKey) — one record per pull request. Looked up by ticket, by the run it
/// dies with, and by (RepoKey, Number) for the PR sweep's join. The URL is unindexed.
/// </summary>
public sealed class PrReviewRecordConfiguration : IEntityTypeConfiguration<PrReviewRecord>
{
    public void Configure(EntityTypeBuilder<PrReviewRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrReviewRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.PrKey).HasMaxLength(PersistenceLimits.Sha256Hex);
        builder.Property(r => r.Repo).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.RepoKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Number).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.State).HasMaxLength(32);
        builder.Property(r => r.HeadSha).HasMaxLength(64);
        builder.HasIndex(r => r.PrKey).IsUnique();
        builder.HasIndex(r => new { r.Project, r.TicketId });
        builder.HasIndex(r => r.RunId);
        builder.HasIndex(r => new { r.RepoKey, r.Number });
    }
}
