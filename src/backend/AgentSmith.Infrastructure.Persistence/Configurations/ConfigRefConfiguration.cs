using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0349: config_ref — the reference graph edge. The target FKs back to
/// config_entity's (EntityType, EntityId) alternate key with ON DELETE RESTRICT,
/// so a referenced entity cannot be deleted while an edge points at it. Both ends
/// are indexed: the target for the "used by" referencing-set query, the source for
/// edge cleanup when the referrer is deleted.
/// </summary>
public sealed class ConfigRefConfiguration : IEntityTypeConfiguration<ConfigRef>
{
    public void Configure(EntityTypeBuilder<ConfigRef> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FromType).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.FromId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.ToType).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.ToId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => new { r.ToType, r.ToId });
        builder.HasIndex(r => new { r.FromType, r.FromId });
        builder.HasOne<ConfigEntity>()
            .WithMany()
            .HasForeignKey(r => new { r.ToType, r.ToId })
            .HasPrincipalKey(e => new { e.EntityType, e.EntityId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
