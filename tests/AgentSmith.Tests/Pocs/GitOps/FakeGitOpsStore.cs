using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one PoC row and its revisions in memory.</summary>
internal sealed class FakeGitOpsStore : IPocGitOpsStore
{
    public PocGitOpsSubject Poc { get; set; } = new(7, "shop", "poc-ann", "main", 1, null, null, null, null, null);

    public Dictionary<int, PocRenderRevision> Revisions { get; } = new() { [2] = new(2, 8080, "abc", PocRenderRig.Digest) };

    public int Records { get; private set; }

    public Task<PocGitOpsSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult<PocGitOpsSubject?>(pocId == Poc.PocId ? Poc : null);

    public Task<PocRenderRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        Task.FromResult(Revisions.GetValueOrDefault(revision));

    public Task RecordDeployedAsync(long pocId, PocDesiredState desired, string? sealFingerprint, CancellationToken ct)
    {
        Records++;
        Poc = Poc with
        {
            Replicas = desired.Replicas, Revision = desired.Revision, ImageDigest = desired.ImageDigest,
            ReadinessPath = desired.ReadinessPath, SealFingerprint = sealFingerprint,
        };
        return Task.CompletedTask;
    }

    public int Absents { get; private set; }

    public Task RecordAbsentAsync(long pocId, CancellationToken ct)
    {
        Absents++;
        Poc = Poc with { Revision = null, ImageDigest = null, SealFingerprint = null };
        return Task.CompletedTask;
    }

    public Task ClearSealFingerprintAsync(long pocId, CancellationToken ct)
    {
        Poc = Poc with { SealFingerprint = null };
        return Task.CompletedTask;
    }
}
