using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0320c: the capacity queue. UNIQUE(Project, TicketId) makes "one queued entry
/// per ticket" a database guarantee (the retry storm produced N rows exactly
/// because nothing constrained it); the identity key is the FIFO order. Indexed
/// strings capped for MySQL utf8mb4, like ActiveRun. ReservedRunId is a plain
/// column, not an enforced FK — the queued Run row is written in the same save,
/// but a projector-side upsert may arrive in either order.
/// </summary>
public sealed class QueuedTicketConfiguration : IEntityTypeConfiguration<QueuedTicket>
{
    public void Configure(EntityTypeBuilder<QueuedTicket> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(q => q.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(q => q.Pipeline).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(q => q.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(q => q.ReservedRunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(q => new { q.Project, q.TicketId }).IsUnique();
    }
}
