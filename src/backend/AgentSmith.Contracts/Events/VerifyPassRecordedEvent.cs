using AgentSmith.Contracts.Verify;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-17-d6fbc: one verify pass (Build and test) as data — per repository the stages it ran and how they came
/// out, the contexts whose verify block was derived from files that have moved since, and the pass's own result
/// (<see cref="VerifyPassResults"/>). Published in the verify step, so <see cref="RunEvent.OriginStepIndex"/> names
/// the step it belongs to. The step's result message keeps its prose for every other reader.
/// </summary>
public sealed record VerifyPassRecordedEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string Result,
    IReadOnlyList<VerifyRepositoryPass> Repositories,
    IReadOnlyList<VerifyStaleDerivation> StaleDerivations)
    : RunEvent(RunId, EventType.VerifyPassRecorded, Timestamp);
