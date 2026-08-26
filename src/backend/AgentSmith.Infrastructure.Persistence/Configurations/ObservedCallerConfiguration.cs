using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-08-26-7a51: one row per subject. Seeing a caller again refreshes the row rather
/// than growing a history — the surface asks "who is there", never "how often".
/// </summary>
public sealed class ObservedCallerConfiguration : IEntityTypeConfiguration<ObservedCallerEntity>
{
    public void Configure(EntityTypeBuilder<ObservedCallerEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(e => e.Subject);
        builder.Property(e => e.Subject).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.NameClaim).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.NameValue).HasMaxLength(PersistenceLimits.IndexedString);
        // No index on LastSeen: SQLite cannot translate a DateTimeOffset comparison, so the
        // retention sweep materialises and filters client-side on every provider, and an
        // index no query can use is a write cost with no reader.
    }
}
