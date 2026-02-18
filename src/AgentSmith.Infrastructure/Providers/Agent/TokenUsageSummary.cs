namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Immutable summary of token usage across all iterations of an agentic execution.
/// </summary>
public sealed record TokenUsageSummary(
    int TotalInputTokens,
    int TotalOutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    int Iterations)
{
    public double CacheHitRate => (TotalInputTokens + CacheReadTokens) > 0
        ? (double)CacheReadTokens / (TotalInputTokens + CacheReadTokens)
        : 0.0;
}
