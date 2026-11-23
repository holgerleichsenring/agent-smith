using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-23-933da: a PoC's audit trail, read in time order, and one entry per (run, act) so a projection replay
/// inserts nothing. An entry no run wrote carries a null RunId, which the unique index admits any number of (filtered
/// IS NOT NULL on SQL Server by EF Core's convention).
/// </summary>
public sealed class PocAuditEntryConfiguration : IEntityTypeConfiguration<PocAuditEntry>
{
    /// <summary>The longest reason an entry keeps.</summary>
    public const int ReasonLength = 500; // not a limit: a column width; the acts that write a reason refuse a longer one by name

    public void Configure(EntityTypeBuilder<PocAuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PocAuditEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Actor).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Act).HasMaxLength(32);
        builder.Property(e => e.Reason).HasMaxLength(ReasonLength);
        builder.Property(e => e.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(e => new { e.PocId, e.AtTicks });
        builder.HasIndex(e => new { e.RunId, e.Act }).IsUnique();
    }
}
