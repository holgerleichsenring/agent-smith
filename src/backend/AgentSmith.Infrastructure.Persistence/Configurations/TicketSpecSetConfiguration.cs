using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0393a: UNIQUE(Project, SpecKey) makes "one pointer per ticket" a database
/// guarantee — the pointer is read on every re-entry to decide whether the spec
/// on the branch is this system's own last revision or a reviewer's edit, and two
/// rows would make that question unanswerable. Indexed strings capped for MySQL
/// utf8mb4, like QueuedTicket.
/// </summary>
public sealed class TicketSpecSetConfiguration : IEntityTypeConfiguration<TicketSpecSet>
{
    public void Configure(EntityTypeBuilder<TicketSpecSet> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.SpecKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.CarryingRepo).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.RevisionSha).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.HandbackSourceSha).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(t => new { t.Project, t.SpecKey }).IsUnique();
    }
}
