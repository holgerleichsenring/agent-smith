using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-10-fcd7a: a project's memories — one row per slug, cascading away with the project's config entity.
/// The text columns are bounded where the repository refuses longer text.
/// </summary>
public sealed class ProjectMemoryConfiguration : IEntityTypeConfiguration<ProjectMemory>
{
    private const int Short = 32;

    public void Configure(EntityTypeBuilder<ProjectMemory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProjectMemories");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Slug).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(m => m.Type).HasMaxLength(Short);
        builder.Property(m => m.Description).HasMaxLength(ProjectMemoryLimits.Description);
        builder.Property(m => m.Body).HasMaxLength(ProjectMemoryLimits.Body);
        builder.Property(m => m.SourceKind).HasMaxLength(Short);
        builder.Property(m => m.SourceId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(m => m.SourceRef).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(m => m.Author).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(m => new { m.ProjectId, m.Slug }).IsUnique();
        builder.HasOne<ConfigEntity>()
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
