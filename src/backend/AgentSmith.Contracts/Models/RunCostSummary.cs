namespace AgentSmith.Contracts.Models;

/// <summary>
/// Cost breakdown for a single pipeline run, in USD. <see cref="Phases"/>
/// is the pipeline-total per-phase view (always populated when any LLM
/// call ran); <see cref="PerRepo"/> is the optional per-repo split for
/// multi-repo runs (populated only when any CallCostRecord carried a
/// RepoName — see p0176a).
/// </summary>
public sealed record RunCostSummary(
    IReadOnlyDictionary<string, PhaseCost> Phases,
    decimal TotalCost,
    IReadOnlyDictionary<string, RepoCost>? PerRepo = null);

/// <summary>
/// Cost for a single execution phase.
/// </summary>
public sealed record PhaseCost(
    string Model,
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens,
    int Iterations,
    decimal Cost);

/// <summary>
/// p0176a: per-repo aggregate for multi-repo runs. Phases drills the per-repo
/// records by SkillExecutionPhase so a repo's PR shows the same phase shape
/// as the pipeline total, scoped to that repo.
/// </summary>
public sealed record RepoCost(
    IReadOnlyDictionary<string, PhaseCost> Phases,
    decimal TotalCost);
