using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-08-9e6e: one row per change source.</summary>
public sealed class SweepCursorConfiguration : IEntityTypeConfiguration<SweepCursor>
{
    public void Configure(EntityTypeBuilder<SweepCursor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SweepCursors");
        builder.HasKey(c => c.Source);
        builder.Property(c => c.Source).HasMaxLength(PersistenceLimits.IndexedString);
    }
}
