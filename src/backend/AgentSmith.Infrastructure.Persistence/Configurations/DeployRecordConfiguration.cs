using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-18-3863f: one deploy record per run, looked up by run and selected by whether it settled.</summary>
public sealed class DeployRecordConfiguration : IEntityTypeConfiguration<DeployRecord>
{
    public void Configure(EntityTypeBuilder<DeployRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DeployRecords");
        builder.HasKey(r => r.RunId);
        builder.Property(r => r.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Context).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.ContextRepo).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Environment).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Version).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.Delivery).HasMaxLength(32);
        builder.Property(r => r.Repo).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.PipelineRunId).HasMaxLength(64);
        builder.Property(r => r.PullRequestState).HasMaxLength(32);
        builder.Property(r => r.CiState).HasMaxLength(32);
        builder.HasIndex(r => r.SettledAtTicks);
    }
}
