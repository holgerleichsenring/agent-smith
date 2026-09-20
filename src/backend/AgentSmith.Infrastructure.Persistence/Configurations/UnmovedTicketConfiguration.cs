using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-18-c1a7: UNIQUE(Project, TicketId) — the record answers "will the next claim fail
/// the same way", so a second failure overwrites the first rather than stacking. Indexed
/// strings capped for MySQL utf8mb4, like TicketSpecSet.
/// </summary>
public sealed class UnmovedTicketConfiguration : IEntityTypeConfiguration<UnmovedTicket>
{
    public void Configure(EntityTypeBuilder<UnmovedTicket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.ConfiguredStatus).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(t => new { t.Project, t.TicketId }).IsUnique();
    }
}
