using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-17-f3a8d: the stored secret values, one row per lower-cased name.</summary>
public sealed class StoredSecretConfiguration : IEntityTypeConfiguration<StoredSecret>
{
    public void Configure(EntityTypeBuilder<StoredSecret> builder)
    {
        builder.ToTable("StoredSecrets");
        builder.HasKey(e => e.Name);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.KeyId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.SetBy).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Ciphertext).IsRequired();
        builder.Property(e => e.Nonce).IsRequired();
        builder.Property(e => e.WrappedKey).IsRequired();
        builder.Property(e => e.WrapNonce).IsRequired();
    }
}
