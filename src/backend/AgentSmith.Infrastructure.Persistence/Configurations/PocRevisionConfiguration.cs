using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-23-933db: a revision number is unique per PoC; the upload id is indexed for a retried Start. The
/// manifest, entrypoint, reason, verdict, explanation and overruled ids are unbounded text: their size follows the archive and the rule that refused it.</summary>
public sealed class PocRevisionConfiguration : IEntityTypeConfiguration<PocRevision>
{
    /// <summary>2026-11-23-933dd: the widest pipeline run page stored — a host's run URL is well under it.</summary>
    public const int UrlLength = 1024; // not a limit: a column width; a host's run page URL is far shorter

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
        builder.Property(r => r.CommitSha).HasMaxLength(PersistenceLimits.IndexedString); // 2026-11-23-933dc
        builder.Property(r => r.Digest).HasMaxLength(PersistenceLimits.IndexedString); // 2026-11-23-933dd
        builder.Property(r => r.PipelineRunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(r => r.PipelineUrl).HasMaxLength(UrlLength);
        builder.Property(r => r.BlockedStage).HasMaxLength(16);
        builder.Property(r => r.RolloutState).HasMaxLength(16); // 2026-11-23-c81ec
        builder.Property(r => r.RolloutCommitSha).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(r => new { r.PocId, r.Number }).IsUnique();
        builder.HasIndex(r => r.UploadId);
    }
}
