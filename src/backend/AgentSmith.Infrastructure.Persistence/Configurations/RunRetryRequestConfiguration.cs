using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-13-9a59n: one retry request per run — the run-child RunId index is made UNIQUE here, so two
/// replicas recording a Retry of the same run meet at the index and the loser updates the winner's row.
/// </summary>
public sealed class RunRetryRequestConfiguration : IEntityTypeConfiguration<RunRetryRequest>
{
    public void Configure(EntityTypeBuilder<RunRetryRequest> builder)
    {
        builder.ToTable("RunRetryRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RequestedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => r.RunId).IsUnique();
    }
}
