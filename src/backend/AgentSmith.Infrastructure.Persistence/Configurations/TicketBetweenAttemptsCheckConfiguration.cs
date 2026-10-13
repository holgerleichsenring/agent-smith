using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-13-9a59h: one between-attempts check per last attempt — RunId is UNIQUE, so two replicas
/// checking the same ticket meet at the index and the loser updates the winner's row.
/// </summary>
public sealed class TicketBetweenAttemptsCheckConfiguration : IEntityTypeConfiguration<TicketBetweenAttemptsCheck>
{
    public void Configure(EntityTypeBuilder<TicketBetweenAttemptsCheck> builder)
    {
        builder.ToTable("TicketBetweenAttemptsChecks");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.RunId).IsUnique();
    }
}
