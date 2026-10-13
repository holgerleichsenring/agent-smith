using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-13-9a59b: UNIQUE(PrKey, HostId) — one entry per host object of a pull request.</summary>
public sealed class PrReviewEntryConfiguration : IEntityTypeConfiguration<PrReviewEntry>
{
    public void Configure(EntityTypeBuilder<PrReviewEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrReviewEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PrKey).HasMaxLength(PersistenceLimits.Sha256Hex);
        builder.Property(e => e.HostId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Kind).HasMaxLength(32);
        builder.Property(e => e.Decision).HasMaxLength(64);
        builder.Property(e => e.AuthorLogin).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.ThreadHostId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.File).HasMaxLength(1024);
        builder.Property(e => e.Body).HasMaxLength(PersistenceLimits.ReviewNoteBody);
        builder.HasIndex(e => new { e.PrKey, e.HostId }).IsUnique();
    }
}
