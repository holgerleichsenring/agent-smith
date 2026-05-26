namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of a SpawnPipelineRunsUseCase.ExecuteAsync call. Carries one ClaimResult per
/// repo enqueued (or the single shared rejection result for whole-spawn failures).
/// </summary>
public sealed record SpawnResult(IReadOnlyList<ClaimResult> ClaimResults);
