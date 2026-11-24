namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ed: the Poc row as an act's precondition reads it — its target, its state and its desired state (Revision
/// and ImageDigest null before the first GitOps commit and after a delete commit).</summary>
public sealed record PocActSubject(
    long PocId, string Name, string Target, string State, int Replicas, int? Revision, string? ImageDigest, string? ReadinessPath)
{
    /// <summary>A delete commit landed but its follow has not settled: the row lost its revision in a state only a deployed
    /// PoC takes.</summary>
    public bool IsDeletePending => Revision is null && State is PocStates.Ready or PocStates.Stopped or PocStates.Failed;

    /// <summary>2026-11-23-c81ee: what the PoC's lifetime is computed from; null where a reader does not say.</summary>
    public PocLifetimeFacts? Lifetime { get; init; }

    /// <summary>2026-11-23-c81ee: when the PoC expired, set only while it is expired — PocActPlan refuses its start.</summary>
    public DateTimeOffset? ExpiredOn { get; init; }
}
