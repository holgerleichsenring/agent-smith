using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-08-0781: one row per (project, ticket).</summary>
public sealed class ReworkLedgerConfiguration : IEntityTypeConfiguration<ReworkLedger>
{
    public void Configure(EntityTypeBuilder<ReworkLedger> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReworkLedgers");
        builder.HasKey(l => new { l.Project, l.TicketId });
        builder.Property(l => l.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(l => l.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
    }
}
