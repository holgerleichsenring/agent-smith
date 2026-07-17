namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0203: <see cref="DisplayName"/> carries the operator-facing label from
/// <see cref="Contracts.Commands.CommandDisplayNames"/>. The dashboard
/// renders it instead of the raw <see cref="StepName"/> (which still
/// carries the C# command class name + the optional repo/context suffix
/// composed in PipelineStepRunner). Optional for backward compat — pre-
/// p0203 producers omit it; consumers fall back to <see cref="StepName"/>.
/// </summary>
/// <summary>
/// p0344b: <see cref="StepStartedEvent.CommandName"/> carries the TYPED command
/// name (a <see cref="Contracts.Commands.CommandNames"/> constant, optionally
/// with a ":param" suffix for parameterised rounds). It is persisted on the
/// RunStep row so the server can derive the run-story beats deterministically
/// from command types instead of display-label strings. Optional for backward
/// compat — pre-p0344b producers omit it, and a run whose steps carry no
/// command name serves <c>beats: null</c>.
/// </summary>
public sealed record StepStartedEvent(
    string RunId,
    int StepIndex,
    string StepName,
    int TotalSteps,
    DateTimeOffset Timestamp,
    string? DisplayName = null,
    string? CommandName = null)
    : RunEvent(RunId, EventType.StepStarted, Timestamp);

public sealed record StepFinishedEvent(
    string RunId,
    int StepIndex,
    string Status,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? Reason = null)
    : RunEvent(RunId, EventType.StepFinished, Timestamp);
