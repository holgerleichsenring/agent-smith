using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-20-24bd: one wait record per run.</summary>
public sealed class ModelRunWaitConfiguration : IEntityTypeConfiguration<ModelRunWait>
{
    public void Configure(EntityTypeBuilder<ModelRunWait> builder)
    {
        builder.ToTable("ModelRunWaits"); // no DbSet names it
        builder.HasKey(w => w.Id);
        builder.Property(w => w.RunId).HasMaxLength(PersistenceLimits.IndexedString).IsRequired();
        builder.HasIndex(w => w.RunId).IsUnique();
    }
}
