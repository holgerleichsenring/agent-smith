using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-06-03c7f: UNIQUE(Tracker, TicketKey) makes "one approved series per ticket" a database
/// guarantee — per tracker INSTANCE, because the ticket key carries only the tracker's type. A
/// second approval of the same ticket UPSERTS in place: the row is a versioned source whose
/// approval instant the carried-vs-stored comparison reads, not a log.
/// Indexed strings capped for MySQL utf8mb4, like TicketSeries.
/// </summary>
public sealed class ApprovedSeriesConfiguration : IEntityTypeConfiguration<ApprovedSeries>
{
    public void Configure(EntityTypeBuilder<ApprovedSeries> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(nameof(AgentSmithDbContext.ApprovedSeries));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.TicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.SeriesId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.CarryingRepo).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.ApprovedInConversation).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.ApprovedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(a => new { a.Tracker, a.TicketKey }).IsUnique();
        // 2026-09-25-c1f7: the discovery listing asks one tracker for its unsatisfied records
        // every poll cycle; it orders by the primary key, which needs no column of its own here.
        builder.HasIndex(a => new { a.Tracker, a.SatisfiedAt });
    }
}
