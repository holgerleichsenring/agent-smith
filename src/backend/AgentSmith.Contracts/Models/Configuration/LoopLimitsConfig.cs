namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Hard limits of the agentic loops, bound from the agentsmith.yml <c>limits:</c> section (and
/// the Studio's limits document). All limits are global; per-project overrides are out of scope.
/// 2026-11-17-274cb: the per-investigator and per-verifier tool caps, the LLM-call, input-token
/// and skills-per-stage caps and the skill-call concurrency are retired — the skill-call runtime
/// that read them had no consumer, so none of them bounded anything. The run's cost cap is the budget.
/// </summary>
public sealed class LoopLimitsConfig
{
    /// <summary>Tool-loop iterations of one scout call (the client call-site reading and the
    /// registry-auth staging), so a model that keeps calling tools cannot hold the step forever.</summary>
    public int MaxToolCallsPerSkill { get; set; } = 30;

    /// <summary>Wall-clock bound of one scout call; a call past it ends with a TimeoutException
    /// that names this key (2026-11-17-274ca).</summary>
    public int MaxSecondsPerSkillCall { get; set; } = 300;

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
    /// tasks return Failed without an LLM call. Defaults to 40. 2026-11-19-55ec: every spec now has a builder and a
    /// fresh reviewer, and a re-review after a larger change, all from one run-wide budget.
    /// </summary>
    public int MaxSubAgentsPerRun { get; set; } = 40;

    /// <summary>
    /// 2026-09-22-5891: how many sub-agents ONE design turn may fan out into. A design turn
    /// is a run of its own, so the run-wide number above would let a single conversation
    /// turn open twenty children; this is the number the design surface gates on and the
    /// capacity of the budget it constructs for itself. Defaults to 8 — two waves at
    /// <see cref="MaxConcurrentSubAgents"/>: readers, then the reviewer and one re-review (2026-11-19-55ec). Set to 0 to take the spawn tool off the design
    /// surface entirely.
    /// </summary>
    public int MaxSubAgentsPerDialogTurn { get; set; } = 8;
}
