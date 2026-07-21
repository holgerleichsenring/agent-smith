namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for context compaction in the agentic loop.
/// Controls when and how old conversation history is summarized to prevent unbounded growth.
/// </summary>
public sealed class CompactionConfig
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// DEPRECATED no-op (p0357). The iteration branch never reset its counter, so past
    /// the threshold EVERY call compacted — per-iteration cache invalidation plus a
    /// summarizer call per iteration, and long runs spent their life in the amnesia
    /// regime. Token pressure (<see cref="MaxContextTokensTriggerRatio"/>) is the only
    /// trigger now. Still parsed so existing agentsmith.yml files load; a non-default
    /// value logs a deprecation warning at startup and is otherwise ignored.
    /// </summary>
    public int ThresholdIterations { get; set; } = DefaultThresholdIterations;

    /// <summary>The historical default — used only to detect an explicitly configured
    /// (and now ignored) value for the deprecation warning.</summary>
    public const int DefaultThresholdIterations = 8;

    /// <summary>
    /// p0357: default raised 80k → 200k — use the window the models actually have.
    /// 80k starved large multi-repo runs whose pinned head alone (skill + plan +
    /// principles + ledger + ticket) is a five-figure token count.
    /// </summary>
    public int MaxContextTokens { get; set; } = 200000;

    /// <summary>
    /// The compaction trigger: fires when accumulated input tokens exceed
    /// <c>ratio × <see cref="MaxContextTokens"/></c>. Default 0.7 leaves 30% headroom for the next
    /// response. Set to 0 (or any non-positive value) to disable compaction triggering
    /// entirely (p0357: there is no iteration fallback anymore).
    /// </summary>
    public double MaxContextTokensTriggerRatio { get; set; } = 0.7;

    public int KeepRecentIterations { get; set; } = 3;
    public string SummaryModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>
    /// Optional deployment-name override for the compactor's summarization call (OpenAI / Azure OpenAI).
    /// Null falls back to the agent's Primary deployment. Set to a smaller-model deployment
    /// (e.g. <c>gpt-4o-mini-deployment</c>) to reduce compaction overhead. Compaction is summarization,
    /// which doesn't need the full primary-task model.
    /// </summary>
    public string? DeploymentName { get; set; }
}
