using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-23-933db: a revision number is unique per PoC; the upload id is indexed for a retried Start. The
/// manifest, entrypoint and reason are unbounded text: their size follows the archive and the rule that refused it.</summary>
public sealed class PocRevisionConfiguration : IEntityTypeConfiguration<PocRevision>
{
    public void Configure(EntityTypeBuilder<PocRevision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PocRevisions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UploadSession).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.UploadId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.State).HasMaxLength(32);
        builder.Property(r => r.Actor).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.BaseImage).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => new { r.PocId, r.Number }).IsUnique();
        builder.HasIndex(r => r.UploadId);
    }
}
