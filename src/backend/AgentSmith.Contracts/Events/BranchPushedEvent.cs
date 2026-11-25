namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-25-098f: a run is about to push <paramref name="Sha"/> to <paramref name="Branch"/> — published before the push, so
/// no read of the host sees the head before agent-smith's record of it. The run row family appends the sha to the run's
/// pushed heads; a moved head found there is agent-smith's own push, any other is a person's.
/// </summary>
public sealed record BranchPushedEvent(string RunId, DateTimeOffset Timestamp, string Branch, string Sha)
    : RunEvent(RunId, EventType.BranchPushed, Timestamp);
