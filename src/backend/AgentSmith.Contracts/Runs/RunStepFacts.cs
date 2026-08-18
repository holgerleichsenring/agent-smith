namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: the little a fold over the trail needs to know about a step that the events
/// themselves cannot say — which phase it belonged to, and how long it took.
/// <para>
/// An event carries the step it was produced in, not the phase: the phase lives in the
/// step's name (p0395). Passing the mapping in keeps the derivation free of the read
/// stack that produces it, so it folds the same way in a test as it does on the server.
/// </para>
/// </summary>
public sealed record RunStepFacts(
    int StepIndex, string? PhaseId, long DurationMs, string? Name = null);
