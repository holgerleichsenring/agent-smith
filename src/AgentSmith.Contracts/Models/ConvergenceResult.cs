namespace AgentSmith.Contracts.Models;

/// <summary>
/// Structured result of convergence analysis across all skill observations.
/// Replaces the free-text ConsolidatedPlan string for discussion pipelines.
/// </summary>
public sealed record ConvergenceResult(
    bool Consensus,
    IReadOnlyList<SkillObservation> Observations,
    IReadOnlyList<ObservationLink> Links,
    IReadOnlyList<string> AdditionalRoles,
    IReadOnlyList<SkillObservation> Blocking,
    IReadOnlyList<SkillObservation> NonBlocking);
