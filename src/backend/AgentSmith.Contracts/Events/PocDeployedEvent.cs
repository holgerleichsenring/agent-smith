namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-23-c81eb: a poc-deploy run's GitOps commit ended well — it committed revision <paramref name="Revision"/>
/// with image <paramref name="Digest"/> into the PoC's deploy folder (<paramref name="CommitSha"/>), or found the folder
/// already holding it (no sha). Published on every such end, so a retry publishes again; its projection writes the
/// PoC's 'deployed' audit entry once per run. <paramref name="Actor"/> is the principal who started the import.
/// </summary>
public sealed record PocDeployedEvent(
    string RunId, DateTimeOffset Timestamp, long PocId, int Revision, string Digest, string? CommitSha, string Actor)
    : RunEvent(RunId, EventType.PocDeployed, Timestamp);
