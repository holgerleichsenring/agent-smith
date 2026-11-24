using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-17-f3a8d: the stored secret values, one row per lower-cased name. 2026-11-23-933dg: one row per
/// (scope, name) — 32 + 200 characters, 464 bytes as nvarchar, under SQL Server's 900-byte index key; an existing row
/// reads as the installation's.</summary>
public sealed class StoredSecretConfiguration : IEntityTypeConfiguration<StoredSecret>
{
    public void Configure(EntityTypeBuilder<StoredSecret> builder)
    {
        builder.ToTable("StoredSecrets");
        builder.HasKey(e => new { e.Scope, e.Name });
        builder.Property(e => e.Scope).HasMaxLength(SecretScope.MaxLength).HasDefaultValue(SecretScope.InstallationText);
        builder.Property(e => e.Name).HasMaxLength(SecretScope.NameMaxLength);
        builder.Property(e => e.KeyId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.SetBy).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Ciphertext).IsRequired();
        builder.Property(e => e.Nonce).IsRequired();
        builder.Property(e => e.WrappedKey).IsRequired();
        builder.Property(e => e.WrapNonce).IsRequired();
    }
}
