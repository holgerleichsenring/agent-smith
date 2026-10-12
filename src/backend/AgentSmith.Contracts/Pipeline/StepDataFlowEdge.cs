namespace AgentSmith.Contracts.Pipeline;

/// <summary>
/// One declared data-flow edge between two pipeline preset steps. The producing
/// step (FromStep) writes the listed ContextKeys; the consuming step
/// (ToStep) is permitted to read them. <c>FromStep == "*"</c> matches
/// any prior step (used for keys produced by infrastructure/initializers that
/// every step may consume — e.g. ResolvedPipeline, Sandbox, RunId).
/// </summary>
public sealed record StepDataFlowEdge(
    string FromStep,
    string ToStep,
    IReadOnlyList<string> ContextKeys);
