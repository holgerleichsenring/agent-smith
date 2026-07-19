using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0349: config_entity — surrogate key, natural (EntityType, EntityId) unique
/// index that doubles as the alternate key config_ref FKs to. Doc is an uncapped
/// JSON blob (no length cap, not indexed); the keyed strings are 191-capped.
/// </summary>
public sealed class ConfigEntityConfiguration : IEntityTypeConfiguration<ConfigEntity>
{
    public void Configure(EntityTypeBuilder<ConfigEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.EntityId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.UpdatedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(e => new { e.EntityType, e.EntityId }).IsUnique();
        builder.HasAlternateKey(e => new { e.EntityType, e.EntityId });
    }
}
