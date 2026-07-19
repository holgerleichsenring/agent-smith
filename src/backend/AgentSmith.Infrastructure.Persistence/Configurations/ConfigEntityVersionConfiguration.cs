using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0349: config_entity_version — the append-only audit. Indexed by the entity it
/// tracks plus its version so the history query is ordered and addressable; Doc is
/// an uncapped JSON blob (nullable = delete tombstone).
/// </summary>
public sealed class ConfigEntityVersionConfiguration : IEntityTypeConfiguration<ConfigEntityVersion>
{
    public void Configure(EntityTypeBuilder<ConfigEntityVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.EntityType).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(v => v.EntityId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(v => v.ChangedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(v => new { v.EntityType, v.EntityId, v.Version });
    }
}
