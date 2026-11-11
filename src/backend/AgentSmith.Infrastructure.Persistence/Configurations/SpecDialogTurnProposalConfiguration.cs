using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-10-d854c: one proposal per design turn, keyed by the turn's run id (unique) and read by the conversation's
/// session id. The proposal JSON is declared by nothing — unbounded, each provider's own convention picks the type.
/// </summary>
public sealed class SpecDialogTurnProposalConfiguration : IEntityTypeConfiguration<SpecDialogTurnProposal>
{
    public void Configure(EntityTypeBuilder<SpecDialogTurnProposal> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(p => p.RunId).IsUnique();
        builder.HasIndex(p => p.SessionId);
    }
}
