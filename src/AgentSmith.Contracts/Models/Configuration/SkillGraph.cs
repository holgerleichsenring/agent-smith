namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Deterministic execution graph built from skill orchestration metadata.
/// Stages are executed in order; skills within a stage have no mutual dependency.
/// </summary>
public sealed record SkillGraph(IReadOnlyList<ExecutionStage> Stages);
