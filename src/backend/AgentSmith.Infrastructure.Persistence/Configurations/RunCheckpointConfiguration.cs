using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0327: one checkpoint per run (unique RunId — a re-checkpoint upserts). The
/// pending set is scanned by the resume sweeper via ResumedAt null.
/// </summary>
public sealed class RunCheckpointConfiguration : IEntityTypeConfiguration<RunCheckpoint>
{
    public void Configure(EntityTypeBuilder<RunCheckpoint> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.Pipeline).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.DialogueJobId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(c => c.QuestionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(c => c.RunId).IsUnique();
        builder.HasIndex(c => new { c.DialogueJobId, c.QuestionId });
        builder.HasIndex(c => c.ResumedAt);
    }
}
