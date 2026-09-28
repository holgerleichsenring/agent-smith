using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// UNIQUE(RunId) — a run reports to one thread. The thread index serves the per-thread guard
/// and the answer lookup; ClosedAt serves the scan of open bindings every replica makes.
/// Indexed strings capped for MySQL utf8mb4.
/// </summary>
public sealed class ChatRunBindingConfiguration : IEntityTypeConfiguration<ChatRunBinding>
{
    private const int ReplyEndpointLength = 512;

    public void Configure(EntityTypeBuilder<ChatRunBinding> builder)
    {
        builder.ToTable("ChatRunBindings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(b => b.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(b => b.ChannelId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(b => b.ThreadId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(b => b.RequestedBy).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(b => b.ReplyEndpoint).HasMaxLength(ReplyEndpointLength);
        builder.Property(b => b.QuestionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(b => b.RunId).IsUnique();
        builder.HasIndex(b => new { b.Platform, b.ChannelId, b.ThreadId });
        builder.HasIndex(b => b.ClosedAt);
    }
}
