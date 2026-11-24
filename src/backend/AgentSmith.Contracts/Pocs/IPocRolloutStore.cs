namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81ec: what a PoC run's rollout follow reads and writes — when the run's row started (the clock
/// CancelEnforcer holds it to), whether any revision of the PoC rolled out healthy, the GitOps commit it follows, and its
/// outcome: the revision's rollout state and the Poc state in one update.
/// </summary>
public interface IPocRolloutStore
{
    /// <summary>Run.StartedAt of <paramref name="runId"/>, or null when the run has no row.</summary>
    Task<DateTimeOffset?> RunStartedAtAsync(string runId, CancellationToken ct);

    /// <summary>Whether a revision of the PoC rolled out healthy — the old pods then still serve after a failure.</summary>
    Task<bool> HasHealthyRolloutAsync(long pocId, CancellationToken ct);

    Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct);

    Task RecordOutcomeAsync(PocRolloutOutcome outcome, CancellationToken ct);
}
