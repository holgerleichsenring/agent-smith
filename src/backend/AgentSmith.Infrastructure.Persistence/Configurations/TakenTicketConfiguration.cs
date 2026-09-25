using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-25-b4d9: UNIQUE(Project, TicketId) — the record answers "does this framework owe
/// this ticket a run", so a second claim of the same ticket overwrites rather than stacks.
/// Indexed strings capped for MySQL utf8mb4, like ActiveRun beside it.
/// </summary>
public sealed class TakenTicketConfiguration : IEntityTypeConfiguration<TakenTicket>
{
    public void Configure(EntityTypeBuilder<TakenTicket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Pipeline).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(t => new { t.Project, t.TicketId }).IsUnique();
    }
}
