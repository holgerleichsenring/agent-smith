namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81eb: what the GitOps commit reads and writes on a PoC. <see cref="RecordDeployedAsync"/> is the one
/// writer of the row's desired state and seal fingerprint, called only after the commit landed or the branch was found
/// already holding the render; <see cref="ClearSealFingerprintAsync"/> makes the next run seal again when a sealed file
/// the PoC needs is missing on the branch.
/// </summary>
public interface IPocGitOpsStore
{
    Task<PocGitOpsSubject?> ReadAsync(long pocId, CancellationToken ct);

    Task<PocRenderRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct);

    Task RecordDeployedAsync(long pocId, PocDesiredState desired, string? sealFingerprint, CancellationToken ct);

    Task ClearSealFingerprintAsync(long pocId, CancellationToken ct);
}
