using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-09-0620a: the three tables that keep what model calls were sent. A record is reached by its run (the
/// reader, deletion) and a block row by its hash (the orphan sweep), so those are the indexes.
/// </summary>
public sealed class PromptRecordConfigurations
{
    private const int HashLength = 64;

    public void Apply(ModelBuilder modelBuilder)
    {
        var block = modelBuilder.Entity<PromptBlockRow>();
        block.ToTable("PromptBlocks");
        block.HasKey(b => b.Hash);
        block.Property(b => b.Hash).HasMaxLength(HashLength);
        block.Property(b => b.Kind).HasMaxLength(16);
        block.Property(b => b.Role).HasMaxLength(32);

        var record = modelBuilder.Entity<PromptRecord>();
        record.ToTable("PromptRecords");
        record.HasKey(r => r.Id);
        record.Property(r => r.Id).HasMaxLength(HashLength);
        record.Property(r => r.ParentId).HasMaxLength(HashLength);
        record.Property(r => r.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        record.HasIndex(r => r.RunId);

        var held = modelBuilder.Entity<PromptRecordBlock>();
        held.ToTable("PromptRecordBlocks");
        held.HasKey(b => new { b.RecordId, b.Position });
        held.Property(b => b.RecordId).HasMaxLength(HashLength);
        held.Property(b => b.BlockHash).HasMaxLength(HashLength);
        held.HasIndex(b => b.BlockHash);
    }
}
