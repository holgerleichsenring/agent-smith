using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-01-283da: copies the legacy image rows into the reference files, a batch at a time,
/// under their OWN ids and with their own CreatedAt — forward only, beside an additive migration,
/// because a migration that moved them would strand every replica still reading the old table.
/// <para>
/// THE KEY IS THE CLAIM. A row copied twice — a lease handover, a second leader — is a primary
/// key violation, translated and skipped, so no interleaving can duplicate an image. And a copy
/// whose legacy row is gone, because an old replica deleted that conversation after the copy, is
/// removed again: an id below the seed was born legacy and lives only as long as its original.
/// </para>
/// </summary>
public sealed class LegacyAttachmentCopy(
    AgentSmithDbContext db,
    IUniqueViolationTranslator uniqueViolations,
    IdentityInsertSwitch identityInsert,
    ILogger<LegacyAttachmentCopy> logger)
{
    private const int BatchSize = 50;

    /// <summary>Copies up to one batch; returns how many uncopied rows it found.</summary>
    public async Task<int> CopyBatchAsync(CancellationToken ct)
    {
        var batch = await db.Set<SpecDialogAttachment>().AsNoTracking()
            .Where(a => !db.Set<ReferenceFile>().Any(f => f.Id == a.Id))
            .OrderBy(a => a.Id).Take(BatchSize).ToListAsync(ct);
        if (batch.Count == 0) return 0;
        var type = db.Model.FindEntityType(typeof(ReferenceFile))!;
        // SQL Server's identity insertion is a property of the session, so one connection
        // carries the switch, the inserts and the switch back.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await identityInsert.EnableAsync(db, type, ct);
            try { await CopyAllAsync(batch, ct); }
            finally { await identityInsert.DisableAsync(db, type, CancellationToken.None); }
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return batch.Count;
    }

    /// <summary>Removes every copy whose legacy row is gone; returns how many.</summary>
    public Task<int> RemoveOrphansAsync(CancellationToken ct) =>
        db.Set<ReferenceFile>()
            .Where(f => f.Id < ReferenceFileIdentity.Seed
                && !db.Set<SpecDialogAttachment>().Any(a => a.Id == f.Id))
            .ExecuteDeleteAsync(ct);

    private async Task CopyAllAsync(IReadOnlyList<SpecDialogAttachment> batch, CancellationToken ct)
    {
        using var keepStamps = db.SuspendAuditStamping();
        foreach (var row in batch) await TryCopyAsync(row, ct);
    }

    private async Task TryCopyAsync(SpecDialogAttachment row, CancellationToken ct)
    {
        db.Add(CopyOf(row));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (uniqueViolations.IsUniqueViolation(ex))
        {
            logger.LogDebug(ex, "Legacy image {Id} was already copied; skipped.", row.Id);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private static ReferenceFile CopyOf(SpecDialogAttachment row)
    {
        var content = Convert.FromBase64String(row.ContentBase64);
        return new ReferenceFile
        {
            Id = row.Id,
            SessionId = row.SessionId,
            SetId = Guid.NewGuid().ToString("N"),
            Kind = ReferenceFileKind.Image,
            MediaType = row.MediaType,
            Length = content.Length,
            Content = content,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }
}
