namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Hard limits for the per-skill agentic loop. Bound from the agentsmith.yml
/// <c>limits:</c> section. Defaults match Phase B of the runtime design.
/// All limits are global; per-project overrides are intentionally out of scope.
/// </summary>
public sealed class LoopLimitsConfig
{
    public int MaxToolCallsPerSkill { get; set; } = 30;
    public int MaxToolCallsPerInvestigator { get; set; } = 10;
    public int MaxToolCallsPerVerifier { get; set; } = 20;
    public int MaxLlmCallsPerSkill { get; set; } = 15;
    // p0236: per-call accumulated-input-token budget, raised 200k → 500k. A
    // coding master is one long agentic call that re-sends its growing context
    // each iteration, so the accumulated count climbs fast for legitimate
    // multi-file work. (Enforcement is currently observational — RecordLlmCall
    // is not yet wired into the loop — so this is headroom, not the fix for the
    // "A task was cancelled" HTTP-timeout, which is AgentConfig.NetworkTimeoutSeconds.)
    public int MaxInputTokensPerSkillCall { get; set; } = 500_000;
    public int MaxOutputTokensPerSkillCall { get; set; } = 16_000;
    public int MaxSecondsPerSkillCall { get; set; } = 300;
    public int MaxConcurrentSkillCalls { get; set; } = 10;

    /// <summary>
    /// p0127b: post-LLM cap on skills selected per phase. TriageOutputProducer
    /// trims any phase whose LLM-pick exceeds this number using the activates_when
    /// specificity score (higher score wins; ties broken by skill name ascending).
    /// </summary>
    public int MaxSkillsPerPhase { get; set; } = 5;

    /// <summary>
    /// p0177: max concurrent sub-agents in flight under one master. Caps the
    /// SemaphoreSlim used by SubAgentRunner. Defaults to 4 — large enough to
    /// hide single-agent latency on the parallel-capable tasks p0177 targets,
    /// small enough that the per-child token spend stays inside the cost cap.
    /// </summary>
    public int MaxConcurrentSubAgents { get; set; } = 4;

    /// <summary>
    /// p0177: run-wide cap on the total number of sub-agents the master may
    /// spawn across all spawn_agents calls within one run. SubAgentBudget
    /// enforces this via thread-safe TryReserve; once exhausted, surplus
    /// tasks return Failed without an LLM call. Defaults to 20.
    /// </summary>
    public int MaxSubAgentsPerRun { get; set; } = 20;

    /// <summary>
    /// 2026-09-22-5891: how many sub-agents ONE design turn may fan out into. A design turn
    /// is a run of its own, so the run-wide number above would let a single conversation
    /// turn open twenty children; this is the number the design surface gates on and the
    /// capacity of the budget it constructs for itself. Defaults to 4 — one wave at
    /// <see cref="MaxConcurrentSubAgents"/>. Set to 0 to take the spawn tool off the design
    /// surface entirely.
    /// </summary>
    public int MaxSubAgentsPerDialogTurn { get; set; } = 4;

    /// <summary>
    /// 2026-09-22-5891: the per-pass tool-iteration ceiling for a child spawned by a DESIGN
    /// turn. A child gets no governor hooks and reports its spend only when it has finished,
    /// so how far it may go IS the bound on a fan-out — the turn's cost cap is consulted once,
    /// before the wave, and refuses only a LATER one. Deliberately far below
    /// <c>agent.max_sub_agent_loop_iterations</c> (100): a dialog child reads and reports, it
    /// does not carry a bulk cross-repo slice. It sits here rather than beside its sibling in
    /// AgentConfig because that file stands at the 120-line limit, and the fan-out numbers a
    /// design turn is bounded by then read as one block.
    /// </summary>
    public int MaxDialogSubAgentLoopIterations { get; set; } = 20;

    /// <summary>
    /// Returns the per-call tool-call cap for the active investigator mode.
    /// <c>verify_diff</c> → MaxToolCallsPerVerifier; <c>verify_hint</c> / <c>survey</c>
    /// → MaxToolCallsPerInvestigator; null/unknown → MaxToolCallsPerSkill.
    /// </summary>
    public int ResolveToolCallCap(string? investigatorMode) => investigatorMode switch
    {
        "verify_diff" => MaxToolCallsPerVerifier,
        "verify_hint" or "survey" => MaxToolCallsPerInvestigator,
        _ => MaxToolCallsPerSkill
    };
}
