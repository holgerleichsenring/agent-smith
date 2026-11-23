using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933da: the PoC rows, read for the list within the caller's projects and one by id. No write path here:
/// 933db's admission writes the first rows.
/// </summary>
public sealed class PocRepository(IUnitOfWork unitOfWork)
{
    /// <summary>The PoCs of the scope's projects (hidden projects left out), newest first.</summary>
    public async Task<IReadOnlyList<Poc>> ListAsync(ProjectScope scope, CancellationToken ct) =>
        await unitOfWork.Set<Poc>().AsNoTracking().Within(scope).OrderByDescending(p => p.Id).ToListAsync(ct);

    /// <summary>2026-11-23-933dc: each listed PoC's latest revision number, for the link to its revision page.</summary>
    public async Task<IReadOnlyDictionary<long, int>> LatestRevisionsAsync(IReadOnlyList<long> pocIds, CancellationToken ct) =>
        await unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => pocIds.Contains(r.PocId)).GroupBy(r => r.PocId)
            .Select(g => new { PocId = g.Key, Latest = g.Max(r => r.Number) }).ToDictionaryAsync(x => x.PocId, x => x.Latest, ct);

    public Task<Poc?> FindAsync(long id, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
}
