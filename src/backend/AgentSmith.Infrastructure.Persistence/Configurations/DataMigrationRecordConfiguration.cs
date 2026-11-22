using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-22-747da: one row per data migration a store has had, keyed by its id.</summary>
public sealed class DataMigrationRecordConfiguration : IEntityTypeConfiguration<DataMigrationRecord>
{
    public void Configure(EntityTypeBuilder<DataMigrationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DataMigrationHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(PersistenceLimits.IndexedString);
    }
}
