namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for context compaction in the agentic loop.
/// Controls when and how old conversation history is summarized to prevent unbounded growth.
/// </summary>
public sealed class CompactionConfig
{
    public bool IsEnabled { get; set; } = true;
    public int ThresholdIterations { get; set; } = 8;
    public int MaxContextTokens { get; set; } = 80000;
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
