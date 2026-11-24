using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-c81ed: the Poc row and one revision as a lifecycle act's precondition reads them. Read only.</summary>
public sealed class PocActRepository(IUnitOfWork unitOfWork)
{
    public Task<PocActSubject?> ReadAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Id == pocId)
            .Select(p => new PocActSubject(p.Id, p.Name, p.Target, p.State, p.Replicas, p.Revision, p.ImageDigest, p.ReadinessPath)
            {
                Lifetime = new PocLifetimeFacts(p.CreatedAt, p.ExpiresAtTicks, p.ReminderSeenAtTicks, p.Extensions), // 2026-11-23-c81ee
            })
            .FirstOrDefaultAsync(ct);

    public Task<PocActRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => r.PocId == pocId && r.Number == revision)
            .Select(r => new PocActRevision(r.Number, r.Digest, r.CommitSha, r.RolloutState))
            .FirstOrDefaultAsync(ct);
}
