namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: what SealPocVariables leaves for the GitOps commit (2026-11-23-c81eb) under ContextKeys.PocSeal —
/// <see cref="Unchanged"/> when the fingerprint equals the one the branch holds (no files then), else the sealed files
/// (poc-secrets when the PoC has a secret variable, poc-pull with pull_mode pull_secret; none when nothing is sealed);
/// the fingerprint to record after the commit lands (null when nothing is sealed); and the checksum the pod template's
/// agent-smith.io/variables-checksum annotation carries. Ciphertext and digests only — what git will hold — so it is
/// checkpointed.
/// </summary>
public sealed record PocSealResult(bool Unchanged, IReadOnlyList<PocSecretFile> Files, string? SealFingerprint, string VariablesChecksum)
{
    /// <summary>2026-11-23-c81eb: whether the PoC has a secret variable — the Deployment then names poc-secrets, never
    /// optional.</summary>
    public bool HasSecrets { get; init; }

    /// <summary>2026-11-23-c81eb: the sealed files the PoC needs in its deploy folder, sealed now or held on the branch —
    /// with <see cref="Unchanged"/> the commit keeps these and fails naming one the branch lost.</summary>
    public IReadOnlyList<string> NeededFiles { get; init; } = [];
}
