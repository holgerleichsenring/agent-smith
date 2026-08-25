using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-08-25-e257: one judgement per run and criterion — recording the same criterion twice
/// replaces the earlier judgement rather than growing a history nobody reads.
/// </summary>
public sealed class RunCriterionJudgementConfiguration
    : IEntityTypeConfiguration<RunCriterionJudgement>
{
    public void Configure(EntityTypeBuilder<RunCriterionJudgement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.CriterionKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.MachineStatus).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.HumanStatus).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(e => e.Author).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(e => new { e.RunId, e.CriterionKey }).IsUnique();
    }
}
