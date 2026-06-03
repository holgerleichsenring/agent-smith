namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0203: <see cref="DisplayName"/> carries the operator-facing label from
/// <see cref="Contracts.Commands.CommandDisplayNames"/>. The dashboard
/// renders it instead of the raw <see cref="StepName"/> (which still
/// carries the C# command class name + the optional repo/context suffix
/// composed in PipelineStepRunner). Optional for backward compat — pre-
/// p0203 producers omit it; consumers fall back to <see cref="StepName"/>.
/// </summary>
public sealed record StepStartedEvent(
    string RunId,
    int StepIndex,
    string StepName,
    int TotalSteps,
    DateTimeOffset Timestamp,
    string? DisplayName = null)
    : RunEvent(RunId, EventType.StepStarted, Timestamp);

public sealed record StepFinishedEvent(
    string RunId,
    int StepIndex,
    string Status,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? Reason = null)
    : RunEvent(RunId, EventType.StepFinished, Timestamp);
