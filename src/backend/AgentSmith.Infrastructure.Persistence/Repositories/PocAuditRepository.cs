using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-933da: a PoC's audit trail, oldest first. Written only by the act projections of later slices.</summary>
public sealed class PocAuditRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PocAuditEntry>> ListAsync(long pocId, CancellationToken ct) =>
        await unitOfWork.Set<PocAuditEntry>().AsNoTracking()
            .Where(e => e.PocId == pocId).OrderBy(e => e.AtTicks).ThenBy(e => e.Id).ToListAsync(ct);

    /// <summary>2026-11-23-c81ee: each listed PoC's latest entry of <paramref name="act"/>, as UTC ticks.</summary>
    public async Task<IReadOnlyDictionary<long, long>> LatestAsync(IReadOnlyList<long> pocIds, string act, CancellationToken ct) =>
        await unitOfWork.Set<PocAuditEntry>().AsNoTracking().Where(e => pocIds.Contains(e.PocId) && e.Act == act).GroupBy(e => e.PocId)
            .Select(g => new { PocId = g.Key, At = g.Max(e => e.AtTicks) }).ToDictionaryAsync(x => x.PocId, x => x.At, ct);
}
