using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// The DB-authoritative ticket status (p0246d). UNIQUE(Project, Platform,
/// TicketId) so each ticket has exactly one lifecycle row; indexed strings are
/// length-capped for the MySQL key-length limit.
/// </summary>
public sealed class TicketLifecycleConfiguration : IEntityTypeConfiguration<TicketLifecycle>
{
    public void Configure(EntityTypeBuilder<TicketLifecycle> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(t => new { t.Project, t.Platform, t.TicketId }).IsUnique();
    }
}
