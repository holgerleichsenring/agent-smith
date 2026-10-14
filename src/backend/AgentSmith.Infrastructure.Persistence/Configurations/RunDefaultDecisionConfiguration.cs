using AgentSmith.Contracts.Decisions;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-13-8762o: the default-decision rows. The unique (RunId, EventSeq) lives in
/// RunRecordIdentityConfiguration, which sees the provider; the conversation is indexed so a ticket finds
/// the defaults of the design turns bound to it.
/// </summary>
public sealed class RunDefaultDecisionConfiguration : IEntityTypeConfiguration<RunDefaultDecision>
{
    private const int Short = 64;

    public void Configure(EntityTypeBuilder<RunDefaultDecision> builder)
    {
        builder.ToTable("RunDefaultDecisions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.SpecId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(d => d.Kind).HasMaxLength(Short);
        builder.Property(d => d.Verdict).HasMaxLength(Short).HasDefaultValue(DefaultDecisionVerdicts.Pending);
        builder.Property(d => d.Conversation).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(d => d.Conversation);
    }
}
