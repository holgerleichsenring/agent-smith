namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of a SpawnPipelineRunsUseCase.ExecuteAsync call. Carries one ClaimResult per
/// repo enqueued (or the single shared rejection result for whole-spawn failures).
/// <para>
/// RunId is the id the run lives under when it was started or deferred — the funnel's own
/// reservation, or the one a ticket already waiting in the capacity queue keeps. Null when
/// the spawn was refused. A caller that has to name the run (a chat reply) reads it here,
/// because a ClaimResult names none.
/// </para>
/// </summary>
public sealed record SpawnResult(IReadOnlyList<ClaimResult> ClaimResults, string? RunId = null);
