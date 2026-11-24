namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81eb: the Poc row as the GitOps commit reads it — its names, its desired state (Revision and
/// ImageDigest null until the first commit), the seal fingerprint the branch holds and the plain variables.</summary>
public sealed record PocGitOpsSubject(
    long PocId, string Name, string Project, string Target, int Replicas, int? Revision, string? ImageDigest,
    string? ReadinessPath, string? SealFingerprint, string? PlainVariablesJson)
{
    public IReadOnlyList<PocPlainVariable> Plain => PocPlainVariables.Parse(PlainVariablesJson);

    /// <summary>2026-11-23-c81ed: the PoC's state (PocStates); null where a reader does not say.</summary>
    public string? State { get; init; }

    /// <summary>2026-11-23-c81ee: what the PoC's lifetime is computed from — poc-deploy keeps an expired PoC stopped.</summary>
    public PocLifetimeFacts? Lifetime { get; init; }

    /// <summary>2026-11-23-c81ed: a render must not reach the deploy folder — the PoC is deleted (a new PoC may own the
    /// folder's name) or its delete commit landed and the follow has not settled (ready, stopped or failed, no revision).</summary>
    public bool IsGone => State == PocStates.Deleted
        || (Revision is null && State is PocStates.Ready or PocStates.Stopped or PocStates.Failed);
}
