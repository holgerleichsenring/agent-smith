using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-14-dbcef: UNIQUE(Tracker, TicketKey, RequiredTicketKey) — one row per edge, so a retried
/// filing cannot store an edge twice. Read by the dependent (the unique index's prefix), by the
/// required ticket and by project. Every string is capped for MySQL utf8mb4.
/// </summary>
public sealed class TicketRequirementConfiguration : IEntityTypeConfiguration<TicketRequirement>
{
    public void Configure(EntityTypeBuilder<TicketRequirement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TicketRequirements");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.TicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.RequiredTicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.RequiredTicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.FiledInConversation).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => new { r.Tracker, r.TicketKey, r.RequiredTicketKey }).IsUnique();
        builder.HasIndex(r => new { r.Tracker, r.RequiredTicketKey });
        builder.HasIndex(r => r.Project);
    }
}
