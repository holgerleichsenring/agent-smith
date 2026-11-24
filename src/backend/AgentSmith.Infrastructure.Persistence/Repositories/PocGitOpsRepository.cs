using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-c81eb: the Poc row and a revision as the GitOps commit reads them, and the one write of the row's
/// desired state and seal fingerprint — a single update, so the two never disagree.</summary>
public sealed class PocGitOpsRepository(IUnitOfWork unitOfWork)
{
    public Task<PocGitOpsSubject?> ReadAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Id == pocId)
            .Select(p => new PocGitOpsSubject(p.Id, p.Name, p.Project, p.Target, p.Replicas, p.Revision, p.ImageDigest,
                p.ReadinessPath, p.SealFingerprint, p.PlainVariablesJson) { State = p.State })
            .FirstOrDefaultAsync(ct);

    public Task<PocRenderRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => r.PocId == pocId && r.Number == revision)
            .Select(r => new PocRenderRevision(r.Number, r.Port, r.CommitSha, r.Digest))
            .FirstOrDefaultAsync(ct);

    public Task RecordDeployedAsync(long pocId, PocDesiredState desired, string? sealFingerprint, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.Replicas, desired.Replicas).SetProperty(p => p.Revision, desired.Revision)
            .SetProperty(p => p.ImageDigest, desired.ImageDigest).SetProperty(p => p.ReadinessPath, desired.ReadinessPath)
            .SetProperty(p => p.SealFingerprint, sealFingerprint), ct);

    /// <summary>2026-11-23-c81ed: after a delete commit — the desired state and the seal fingerprint gone in one update.</summary>
    public Task RecordAbsentAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.Revision, (int?)null).SetProperty(p => p.ImageDigest, (string?)null)
            .SetProperty(p => p.SealFingerprint, (string?)null), ct);

    public Task ClearSealFingerprintAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set.SetProperty(p => p.SealFingerprint, (string?)null), ct);
}
