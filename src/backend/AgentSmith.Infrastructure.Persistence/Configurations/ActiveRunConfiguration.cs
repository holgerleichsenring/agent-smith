using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// The single-run lease. The load-bearing constraint of the whole phase: a real
/// UNIQUE(Project, TicketId) index (NOT filtered — MySQL lacks partial indexes),
/// both columns capped at the indexed-string length so the migration applies on
/// MySQL utf8mb4. This index — not the heartbeat — is what makes a second claim
/// for the same ticket a database error.
/// </summary>
public sealed class ActiveRunConfiguration : IEntityTypeConfiguration<ActiveRun>
{
    public void Configure(EntityTypeBuilder<ActiveRun> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(a => new { a.Project, a.TicketId }).IsUnique();
        builder.HasOne(a => a.Run).WithMany().HasForeignKey(a => a.RunId);
    }
}
