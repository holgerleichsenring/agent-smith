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
    IReadOnlyDictionary<string, RepoCost>? PerRepo = null,
    // p0361: tokens whose model had no resolvable price. Non-null and non-empty
    // means TotalCost is a LOWER BOUND — result.md renders a cost_incomplete
    // block so a missing price can never masquerade as a free run.
    IReadOnlyDictionary<string, long>? UnpricedTokensByModel = null,
    // 2026-09-01-b0d7: what an external agent CLI reported for the calls it answered.
    // Never part of TotalCost — that transport spends no money against an agent budget —
    // and never an unpriced-model alarm, because it has no table price by design.
    WorkerSpend? WorkerSpend = null);

/// <summary>
/// 2026-09-01-b0d7: the tokens and the USD figure an external agent CLI reported for the
/// calls it answered in one run. The cost is the CLI's OWN number and is not comparable
/// to a provider call: its cache-creation tokens are the CLI's system prompt and tool
/// schemas, charged per call, not this run's context.
/// </summary>
public sealed record WorkerSpend(
    string Models,
    int CallCount,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheCreationTokens,
    decimal ReportedCostUsd);

/// <summary>
/// Cost for a single execution phase.
/// <para>
/// 2026-09-01-3653: an Iterations field used to sit here and be printed as
/// <c>turns:</c> in result.md. It was always zero for every scoped phase, because the only
/// thing that ever incremented it — LimitEnforcer.RecordLlmCall — has no production caller
/// anywhere. It is removed rather than repaired: the scan's coverage account now states the
/// turns a pass used, near-exact and counted from the transcript, and two numbers called
/// turns in one file would have contradicted each other.
/// </para>
/// </summary>
public sealed record PhaseCost(
    string Model,
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens,
    decimal Cost);

/// <summary>
/// p0176a: per-repo aggregate for multi-repo runs. Phases drills the per-repo
/// records by SkillExecutionPhase so a repo's PR shows the same phase shape
/// as the pipeline total, scoped to that repo.
/// </summary>
public sealed record RepoCost(
    IReadOnlyDictionary<string, PhaseCost> Phases,
    decimal TotalCost);
