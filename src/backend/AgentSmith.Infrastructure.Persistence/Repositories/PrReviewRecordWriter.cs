using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59b: the refresh's writes. An existing record is written only by compare-and-set on
/// CheckedAt being older than the read's start, so a refresh that outlived its ticket lock never
/// overwrites a newer one; a new record is an insert whose unique violation means a racing refresh
/// won. Entries are upserted by host id and never deleted but with their record; a stored decision
/// the read no longer lists stops standing. Every write answers whether anything a reader shows
/// changed — CheckedAt alone is no change.
/// </summary>
public sealed class PrReviewRecordWriter(IUnitOfWork unitOfWork, IUniqueViolationTranslator violations)
{
    public async Task<bool> WriteAsync(
        PrReviewRecord record, IReadOnlyList<PrReviewEntry> entries, long readStartTicks, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<PrReviewRecord>().FirstOrDefaultAsync(r => r.PrKey == record.PrKey, ct);
        if (existing is null) return await InsertAsync(record, entries, ct);
        var won = await unitOfWork.Set<PrReviewRecord>()
            .Where(r => r.PrKey == record.PrKey && r.CheckedAtTicks < readStartTicks)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CheckedAtTicks, record.CheckedAtTicks), ct);
        if (won == 0) return false;
        PrReviewRecordCopy.Record(record, existing);
        await MergeEntriesAsync(record.PrKey, entries, ct);
        return await unitOfWork.SaveChangesAsync(ct) > 0;
    }

    /// <summary>A host failure on a pull request never read: the record says unread. An existing one is left as it is.</summary>
    public async Task<bool> MarkUnreadAsync(PrReviewRecord record, CancellationToken ct) =>
        !await unitOfWork.Set<PrReviewRecord>().AnyAsync(r => r.PrKey == record.PrKey, ct)
        && await InsertAsync(record, [], ct);

    /// <summary>Records no run of the ticket names any more, with their entries.</summary>
    public async Task DeleteAsync(IReadOnlyCollection<string> prKeys, CancellationToken ct)
    {
        if (prKeys.Count == 0) return;
        await unitOfWork.Set<PrReviewEntry>().Where(e => prKeys.Contains(e.PrKey)).ExecuteDeleteAsync(ct);
        await unitOfWork.Set<PrReviewRecord>().Where(r => prKeys.Contains(r.PrKey)).ExecuteDeleteAsync(ct);
    }

    private async Task<bool> InsertAsync(PrReviewRecord record, IReadOnlyList<PrReviewEntry> entries, CancellationToken ct)
    {
        var added = new[] { unitOfWork.Add(record) }.Concat(entries.Select(unitOfWork.Add)).ToList();
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            foreach (var entry in added) entry.State = EntityState.Detached;
            return false;
        }
    }

    private async Task MergeEntriesAsync(string prKey, IReadOnlyList<PrReviewEntry> entries, CancellationToken ct)
    {
        var stored = await unitOfWork.Set<PrReviewEntry>().Where(e => e.PrKey == prKey).ToListAsync(ct);
        var byHostId = stored.ToDictionary(e => e.HostId, StringComparer.Ordinal);
        foreach (var entry in entries)
            if (byHostId.TryGetValue(entry.HostId, out var row)) PrReviewRecordCopy.Entry(entry, row);
            else unitOfWork.Add(entry);
        var read = entries.Select(e => e.HostId).ToHashSet(StringComparer.Ordinal);
        foreach (var row in stored.Where(r => r.Standing && !read.Contains(r.HostId))) row.Standing = false;
    }
}
