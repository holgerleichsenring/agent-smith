using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-02-5ab2a: one row per connection, keyed by its lower-cased name.
/// </summary>
public sealed class ConnectionDiscoveryConfiguration : IEntityTypeConfiguration<ConnectionDiscovery>
{
    public const int ConnectionNameLength = 200;

    public void Configure(EntityTypeBuilder<ConnectionDiscovery> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ConnectionDiscoveries");
        builder.HasKey(e => e.ConnectionName);
        builder.Property(e => e.ConnectionName).HasMaxLength(ConnectionNameLength);
    }
}
