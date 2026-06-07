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
        builder.HasIndex(r => r.Project);
    }
}
