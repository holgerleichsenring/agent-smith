using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-01-283da: the files an operator hands a design conversation. The session index
/// serves the conversation's reads and its delete; the set index serves a set read whole.
/// <para>
/// CONTENT DECLARES NO TYPE, like the legacy image column before it: the shared migration set
/// emits literals three providers run verbatim, so the column is left untyped there and each
/// provider maps an unbounded byte array to its own large binary type.
/// </para>
/// <para>
/// THE IDENTITY STARTS AT <see cref="ReferenceFileIdentity.Seed"/>. SQL Server carries the seed
/// in its own model and migration, so it is declared here for that provider only; the shared set
/// seeds it per provider in its migration, which is the one place that set can say it.
/// </para>
/// </summary>
public sealed class ReferenceFileConfiguration(string? providerName) : IEntityTypeConfiguration<ReferenceFile>
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    public void Configure(EntityTypeBuilder<ReferenceFile> builder)
    {
        builder.ToTable("ReferenceFiles");
        builder.HasKey(f => f.Id);
        if (providerName == SqlServerProvider)
            builder.Property(f => f.Id).UseIdentityColumn(ReferenceFileIdentity.Seed, 1);
        builder.Property(f => f.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(f => f.SetId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(f => f.Kind).HasMaxLength(ReferenceFileKind.MaxLength);
        builder.Property(f => f.RelativePath).HasMaxLength(PersistenceLimits.ReferencePath);
        builder.Property(f => f.MediaType).HasMaxLength(PersistenceLimits.IndexedString);
        // 2026-10-08-e8b9g: compared within one conversation's rows, which the session index
        // already narrows to — so no index of its own (MySQL could not index TEXT without a prefix).
        builder.Property(f => f.ContentSha256).HasMaxLength(PersistenceLimits.Sha256Hex);
        builder.HasIndex(f => f.SessionId);
        builder.HasIndex(f => f.SetId);
    }
}
