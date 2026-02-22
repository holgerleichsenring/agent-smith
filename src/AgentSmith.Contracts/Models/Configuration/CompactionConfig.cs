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
}
