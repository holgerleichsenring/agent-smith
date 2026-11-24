namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-23-c81ed: a lifecycle act's GitOps commit ended well — <paramref name="Act"/> (PocActKinds) committed into the
/// PoC's deploy folder (<paramref name="CommitSha"/>), or the folder already held it (no sha). Published on every such
/// end, so a retry publishes again; its projection writes the act's audit entry once per run and act. It records the
/// commit, not the rollout: a delete whose follow fails is still audited 'deleted'. <paramref name="Revision"/> is the
/// revision the desired state names (null for a delete).
/// <para>2026-11-23-c81ee: <paramref name="AuditAct"/> is the audit act recorded in place of the act's own — 'expired'
/// for an expiry stop or delete, 'deleted' for a removal, 'extended' for an extension — null for an owner's act.</para>
/// </summary>
public sealed record PocActedEvent(
    string RunId, DateTimeOffset Timestamp, long PocId, string Act, string Actor, string? Reason, int? Revision, string? CommitSha,
    string? AuditAct = null)
    : RunEvent(RunId, EventType.PocActed, Timestamp);
