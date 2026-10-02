using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-02-5ab2d: keyed by (Platform, ChannelId). ChannelId is wide because a Teams
/// conversation id is far longer than a Slack channel id.
/// </summary>
public sealed class PendingClarificationConfiguration : IEntityTypeConfiguration<PendingClarification>
{
    public const int PlatformLength = 32;
    public const int ChannelIdLength = 400;
    public const int TokenLength = 32;

    public void Configure(EntityTypeBuilder<PendingClarification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PendingClarifications");
        builder.HasKey(e => new { e.Platform, e.ChannelId });
        builder.Property(e => e.Platform).HasMaxLength(PlatformLength);
        builder.Property(e => e.ChannelId).HasMaxLength(ChannelIdLength);
        builder.Property(e => e.Token).HasMaxLength(TokenLength);
    }
}
