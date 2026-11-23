using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-23-933da: a live name is unique per target. A deleted PoC carries a null LiveName: SQLite and PostgreSQL
/// allow several NULLs in a unique index, and EF Core's SQL Server provider filters a unique index over a nullable
/// column to IS NOT NULL, so every provider frees a deleted PoC's name.
/// </summary>
public sealed class PocConfiguration : IEntityTypeConfiguration<Poc>
{
    /// <summary>'sha256:' plus 64 hex digits.</summary>
    public const int ImageDigestLength = 71; // not a limit: the fixed width of a sha256 digest reference

    public void Configure(EntityTypeBuilder<Poc> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Pocs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(63);
        builder.Property(p => p.LiveName).HasMaxLength(63);
        builder.Property(p => p.Project).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.Target).HasMaxLength(63);
        builder.Property(p => p.Folder).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.Owner).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.OwnerName).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.State).HasMaxLength(32);
        builder.Property(p => p.ImageDigest).HasMaxLength(ImageDigestLength);
        builder.Property(p => p.LastRunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(p => new { p.Target, p.LiveName }).IsUnique();
        builder.HasIndex(p => p.Project);
    }
}
