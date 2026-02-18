using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Tracks and accumulates token usage across all API calls in an agentic execution,
/// including cache creation and cache read metrics, with per-phase breakdown.
/// </summary>
public sealed class TokenUsageTracker
{
    private int _totalInput;
    private int _totalOutput;
    private int _cacheCreate;
    private int _cacheRead;
    private int _iterations;

    private readonly Dictionary<string, PhaseUsage> _phases = new();
    private string _currentPhase = "primary";

    public void SetPhase(string phase) => _currentPhase = phase;

    public void Track(MessageResponse response)
    {
        var usage = response.Usage;
        _totalInput += usage.InputTokens;
        _totalOutput += usage.OutputTokens;
        _cacheCreate += usage.CacheCreationInputTokens;
        _cacheRead += usage.CacheReadInputTokens;
        _iterations++;

        if (!_phases.TryGetValue(_currentPhase, out var phaseUsage))
        {
            phaseUsage = new PhaseUsage();
            _phases[_currentPhase] = phaseUsage;
        }

        phaseUsage.InputTokens += usage.InputTokens;
        phaseUsage.OutputTokens += usage.OutputTokens;
        phaseUsage.CacheReadTokens += usage.CacheReadInputTokens;
        phaseUsage.Iterations++;
    }

    public TokenUsageSummary GetSummary() => new(
        _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations);

    public IReadOnlyDictionary<string, PhaseUsage> GetPhaseBreakdown() =>
        _phases.AsReadOnly();

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

/// <summary>
/// Token usage for a single execution phase (scout, planning, primary, compaction).
/// </summary>
public sealed class PhaseUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int Iterations { get; set; }
}
