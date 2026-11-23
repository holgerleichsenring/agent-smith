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
}
