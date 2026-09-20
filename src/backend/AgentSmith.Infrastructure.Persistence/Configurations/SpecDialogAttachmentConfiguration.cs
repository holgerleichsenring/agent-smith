using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-20-3af8: a design conversation's images. The session index serves the turn's read
/// and the conversation delete's sweep; both address a conversation, never a single row.
/// <para>
/// CONTENTBASE64 DECLARES NO LENGTH, AND THAT IS THE POINT. The shared migration set is
/// generated from SQLite and emits its column types as LITERALS, which three providers then run
/// verbatim — and on MySQL the literal a bounded string produces means sixty-four kilobytes,
/// about a hundredth of an encoded five-megabyte image. Left unbounded here AND untyped in the
/// migration, each provider's own convention maps it to that provider's large-text type.
/// </para>
/// </summary>
public sealed class SpecDialogAttachmentConfiguration : IEntityTypeConfiguration<SpecDialogAttachment>
{
    public void Configure(EntityTypeBuilder<SpecDialogAttachment> builder)
    {
        // Named like every other table here. The entity carries no DbSet property — nothing
        // reads these rows off the context directly — so the name is stated rather than
        // pluralized from one.
        builder.ToTable("SpecDialogAttachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.MediaType).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(a => a.SessionId);
    }
}
