using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-15-114fa: UNIQUE(Tracker, TicketKey) — one close mark per ticket, overwritten by the newest close.
/// Read by key and by project; the note and the closed pull requests are unindexed.
/// </summary>
public sealed class ClosedTicketConfiguration : IEntityTypeConfiguration<ClosedTicket>
{
    public void Configure(EntityTypeBuilder<ClosedTicket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ClosedTickets");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.TicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.By).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.ClosedPullRequestsJson).HasDefaultValue("[]");
        builder.HasIndex(c => new { c.Tracker, c.TicketKey }).IsUnique();
        builder.HasIndex(c => c.Project);
    }
}
