namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for context compaction in the agentic loop.
/// Controls when and how old conversation history is summarized to prevent unbounded growth.
/// </summary>
public sealed class CompactionConfig
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Defensive upper-bound iteration cap. p0147c flipped the primary trigger from
    /// iteration-count to token-pressure (see <see cref="MaxContextTokensTriggerRatio"/>);
    /// this stays as a backstop because token counts are estimates — a pathological
    /// prompt could keep iteration count low while pushing real tokens over the cap,
    /// or the token estimator could undershoot. Defence in depth.
    /// </summary>
    public int ThresholdIterations { get; set; } = 8;

    public int MaxContextTokens { get; set; } = 80000;

    /// <summary>
    /// p0147c primary compaction trigger: fires when accumulated input tokens exceed
    /// <c>ratio × <see cref="MaxContextTokens"/></c>. Default 0.7 leaves 30% headroom for the next
    /// response. Set to 0 (or any non-positive value) to disable the token-ratio trigger
    /// and fall back to iteration-cap-only behaviour.
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
