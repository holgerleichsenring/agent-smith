using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-08-10b0: one row per swept repository.</summary>
public sealed class PrSweepRepositoryConfiguration : IEntityTypeConfiguration<PrSweepRepository>
{
    public void Configure(EntityTypeBuilder<PrSweepRepository> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrSweepRepositories");
        builder.HasKey(r => r.Repository);
        builder.Property(r => r.Repository).HasMaxLength(PersistenceLimits.IndexedString);
    }
}
