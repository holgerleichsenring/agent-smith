using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-14-dbceg: UNIQUE(Tracker, TicketKey) — one landing per ticket, so of two replicas seeing
/// one merge exactly one insert wins. Read by key and by project; the note and the URL are unindexed.
/// </summary>
public sealed class LandedTicketConfiguration : IEntityTypeConfiguration<LandedTicket>
{
    public void Configure(EntityTypeBuilder<LandedTicket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LandedTickets");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(l => l.TicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(l => l.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(l => l.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(l => l.Kind).HasMaxLength(16);
        builder.Property(l => l.By).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(l => new { l.Tracker, l.TicketKey }).IsUnique();
        builder.HasIndex(l => l.Project);
    }
}
