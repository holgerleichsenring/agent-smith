using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-20-24bd: UNIQUE (Key, Slot) is the guard a place is taken against; RunId is the release's lookup.</summary>
public sealed class ModelRunSlotConfiguration : IEntityTypeConfiguration<ModelRunSlot>
{
    public void Configure(EntityTypeBuilder<ModelRunSlot> builder)
    {
        builder.ToTable("ModelRunSlots"); // no DbSet names it
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Key).HasMaxLength(ModelRunSlotKey.MaxLength).IsRequired();
        builder.Property(s => s.RunId).HasMaxLength(PersistenceLimits.IndexedString).IsRequired();
        builder.HasIndex(s => new { s.Key, s.Slot }).IsUnique();
        builder.HasIndex(s => s.RunId);
    }
}
