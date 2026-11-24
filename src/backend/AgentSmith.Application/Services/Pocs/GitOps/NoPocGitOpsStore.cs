using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: without a database no Poc row exists to deploy.</summary>
public sealed class NoPocGitOpsStore : IPocGitOpsStore
{
    public Task<PocGitOpsSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult<PocGitOpsSubject?>(null);

    public Task<PocRenderRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        Task.FromResult<PocRenderRevision?>(null);

    public Task RecordDeployedAsync(long pocId, PocDesiredState desired, string? sealFingerprint, CancellationToken ct) =>
        Task.CompletedTask;

    public Task ClearSealFingerprintAsync(long pocId, CancellationToken ct) => Task.CompletedTask;
}
