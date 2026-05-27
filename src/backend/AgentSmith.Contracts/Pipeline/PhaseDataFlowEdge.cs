namespace AgentSmith.Contracts.Pipeline;

/// <summary>
/// One declared data-flow edge between two pipeline preset steps. The producing
/// step (FromPhaseStep) writes the listed ContextKeys; the consuming step
/// (ToPhaseStep) is permitted to read them. <c>FromPhaseStep == "*"</c> matches
/// any prior step (used for keys produced by infrastructure/initializers that
/// every step may consume — e.g. ResolvedPipeline, Sandbox, RunId).
/// </summary>
public sealed record PhaseDataFlowEdge(
    string FromPhaseStep,
    string ToPhaseStep,
    IReadOnlyList<string> ContextKeys);
