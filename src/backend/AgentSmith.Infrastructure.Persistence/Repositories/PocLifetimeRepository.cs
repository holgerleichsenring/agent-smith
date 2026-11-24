using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-c81ee: the PoCs as their lifetime is read — rows, the latest 'expired' audit entry per PoC (a long AtTicks,
/// over 933da's (PocId, AtTicks) index) and, for a blocked PoC that never deployed, whether its latest revision's stored
/// upload is still in the byte store. No query compares a DateTimeOffset (SQLite cannot translate it): every expiry is
/// computed in memory by the caller. The two writes are an extension and the owner's seen reminder.
/// </summary>
public sealed class PocLifetimeRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PocLifetimeSubject>> LiveAsync(CancellationToken ct) =>
        await SubjectsAsync(unitOfWork.Set<Poc>().AsNoTracking()
            .Where(p => p.State != PocStates.Refused && p.State != PocStates.Deleted).OrderBy(p => p.Id), ct);

    public async Task<PocLifetimeSubject?> ReadAsync(long pocId, CancellationToken ct) =>
        (await SubjectsAsync(unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Id == pocId), ct)).FirstOrDefault();

    public Task ExtendAsync(long pocId, long expiresAtTicks, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.Extensions, p => p.Extensions + 1).SetProperty(p => p.ExpiresAtTicks, expiresAtTicks), ct);

    public Task RecordReminderSeenAsync(long pocId, long seenAtTicks, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set.SetProperty(p => p.ReminderSeenAtTicks, seenAtTicks), ct);

    private async Task<IReadOnlyList<PocLifetimeSubject>> SubjectsAsync(IQueryable<Poc> query, CancellationToken ct)
    {
        var rows = await query.Select(p => new
        {
            p.Id, p.Name, p.Target, p.State, p.Replicas, p.Revision, p.CreatedAt, p.ExpiresAtTicks, p.ReminderSeenAtTicks,
            p.Extensions, p.LastRunId,
        }).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var expired = await unitOfWork.Set<PocAuditEntry>().AsNoTracking().Where(a => ids.Contains(a.PocId) && a.Act == PocActs.Expired)
            .GroupBy(a => a.PocId).Select(g => new { PocId = g.Key, At = g.Max(a => a.AtTicks) }).ToDictionaryAsync(x => x.PocId, x => x.At, ct);
        var gone = await UploadsGoneAsync([.. rows.Where(r => r.State == PocStates.Blocked && r.Revision is null).Select(r => r.Id)], ct);
        return [.. rows.Select(r => new PocLifetimeSubject(r.Id, r.Name, r.Target, r.State, r.Replicas, r.Revision,
            new PocLifetimeFacts(r.CreatedAt, r.ExpiresAtTicks, r.ReminderSeenAtTicks, r.Extensions),
            expired.TryGetValue(r.Id, out var at) ? at : null, gone.Contains(r.Id), r.LastRunId))];
    }

    // The PoCs among pocIds whose latest revision's stored upload is no longer in the byte store.
    private async Task<HashSet<long>> UploadsGoneAsync(IReadOnlyList<long> pocIds, CancellationToken ct)
    {
        if (pocIds.Count == 0) return [];
        var latest = (await unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => pocIds.Contains(r.PocId))
                .Select(r => new { r.PocId, r.Number, r.UploadSession, r.UploadId }).ToListAsync(ct))
            .GroupBy(r => r.PocId).Select(g => g.MaxBy(r => r.Number)!).ToList();
        var sets = latest.Select(r => r.UploadId).ToList();
        var stored = (await unitOfWork.Set<ReferenceFile>().AsNoTracking().Where(f => sets.Contains(f.SetId))
            .Select(f => new { f.SessionId, f.SetId }).Distinct().ToListAsync(ct)).Select(f => (f.SessionId, f.SetId)).ToHashSet();
        return [.. latest.Where(r => !stored.Contains((r.UploadSession, r.UploadId))).Select(r => r.PocId)];
    }
}
