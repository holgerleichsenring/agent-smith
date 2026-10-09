using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-08-0781: one row per (project, ticket); due time indexed for the worker's read.</summary>
public sealed class ReworkNudgeConfiguration : IEntityTypeConfiguration<ReworkNudge>
{
    public void Configure(EntityTypeBuilder<ReworkNudge> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReworkNudges");
        builder.HasKey(n => new { n.Project, n.TicketId });
        builder.Property(n => n.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(n => n.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(n => n.Channel).HasMaxLength(32);
        builder.Property(n => n.ClaimToken).HasMaxLength(64);
        builder.HasIndex(n => n.DueTicks);
    }
}
