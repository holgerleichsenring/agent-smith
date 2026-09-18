using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// Run is keyed by the sortable string run id; the PK column gets an explicit
/// MaxLength (a string key without one fails the MySQL index-length check).
/// Project / TicketId are bounded too (they are queried + indexed downstream).
/// </summary>
public sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        // p0330: bounded like the other id-shaped columns (12-char spawn handle today).
        builder.Property(r => r.JobId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => r.Project);
        // 2026-09-17-042ej: the filed-work read looks a WORK TICKET up across the projects of one
        // tracker, and runs on every nudge window of a live run. On the Project index alone each
        // lookup scanned every run that project ever had.
        builder.HasIndex(r => new { r.Project, r.TicketId });
    }
}
