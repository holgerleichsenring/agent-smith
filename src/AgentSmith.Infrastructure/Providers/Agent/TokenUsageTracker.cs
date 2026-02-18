using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Tracks and accumulates token usage across all API calls in an agentic execution,
/// including cache creation and cache read metrics.
/// </summary>
public sealed class TokenUsageTracker
{
    private int _totalInput;
    private int _totalOutput;
    private int _cacheCreate;
    private int _cacheRead;
    private int _iterations;

    public void Track(MessageResponse response)
    {
        var usage = response.Usage;
        _totalInput += usage.InputTokens;
        _totalOutput += usage.OutputTokens;
        _cacheCreate += usage.CacheCreationInputTokens;
        _cacheRead += usage.CacheReadInputTokens;
        _iterations++;
    }

    public TokenUsageSummary GetSummary() => new(
        _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations);

    public void LogSummary(ILogger logger)
    {
        var summary = GetSummary();
        logger.LogInformation(
            "Token usage summary: {Input} input, {Output} output, " +
            "{CacheCreate} cache-create, {CacheRead} cache-read, " +
            "Cache hit rate: {Rate:P1}, Iterations: {Iter}",
            summary.TotalInputTokens,
            summary.TotalOutputTokens,
            summary.CacheCreationTokens,
            summary.CacheReadTokens,
            summary.CacheHitRate,
            summary.Iterations);
    }
}
