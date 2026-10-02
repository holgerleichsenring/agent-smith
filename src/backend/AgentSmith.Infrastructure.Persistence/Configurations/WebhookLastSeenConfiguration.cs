using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-10-02-5ab2e: one row per platform.</summary>
public sealed class WebhookLastSeenConfiguration : IEntityTypeConfiguration<WebhookLastSeen>
{
    public const int PlatformLength = 32;

    public void Configure(EntityTypeBuilder<WebhookLastSeen> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WebhookLastSeen");
        builder.HasKey(e => e.Platform);
        builder.Property(e => e.Platform).HasMaxLength(PlatformLength);
    }
}
