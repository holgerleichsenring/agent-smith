using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-17-0e79a: UNIQUE(Tracker, SpecKey) makes "one approved record per ticket" a database
/// guarantee — per tracker INSTANCE, because the spec key carries only the tracker's type. A second approval of the same ticket UPSERTS in place — the record is a
/// versioned source whose approval instant is what the precedence compares, not a log, and
/// two rows would make "which approval is current" unanswerable.
/// Indexed strings capped for MySQL utf8mb4, like TicketSpecSet.
/// </summary>
public sealed class ApprovedSpecSetConfiguration : IEntityTypeConfiguration<ApprovedSpecSet>
{
    public void Configure(EntityTypeBuilder<ApprovedSpecSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.SpecKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.ApprovedInConversation).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.ApprovedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(a => new { a.Tracker, a.SpecKey }).IsUnique();
        // 2026-09-25-c1f7: the discovery listing asks one tracker for its unsatisfied records
        // every poll cycle, so that is the shape the index has. It orders by the row id, which is
        // the primary key and needs no column of its own here.
        builder.HasIndex(a => new { a.Tracker, a.SatisfiedAt });
    }
}
