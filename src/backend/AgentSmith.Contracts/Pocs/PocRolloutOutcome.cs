namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ec: what a rollout follow records — the revision's rollout state (PocRolloutStates) and when,
/// and the Poc state to write, or null to keep the state it has (a later rollout failed while the old pods serve).
/// 2026-11-23-c81ed: a null rollout state leaves the revision's untouched (a stop, start or apply changed no digest).</summary>
public sealed record PocRolloutOutcome(long PocId, int Revision, string? RolloutState, DateTimeOffset At, string? PocState);
