namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0405: the executor announcing the steps it is going to run, from
/// <see cref="FirstStepIndex"/> onwards. Published when the command list is
/// established and again whenever a handler splices into it (PhaseSequence turns
/// one command into five per derived phase), so the announcement is always the
/// list the run will actually execute — REPORTED by the producer that holds it,
/// never re-derived by a reader from a preset and a step count.
/// <para>
/// The run row keeps the latest announcement; the rail serves the entries beyond
/// the last executed step as its planned tail. Travels the event stream because a
/// spawned orchestrator has no other DB channel (p0330).
/// </para>
/// <para><see cref="StepsJson"/> is the camelCase wire JSON
/// (<c>RunStoryJson</c>) of an array of <c>PlannedStepView</c>.</para>
/// </summary>
public sealed record PipelineStepsPlannedEvent(
    string RunId,
    int FirstStepIndex,
    string StepsJson,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.PipelineStepsPlanned, Timestamp);
