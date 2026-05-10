using System.Diagnostics;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Tracks tokens-in / tokens-out / wall-clock during a single skill call and
/// enforces the token + wall-clock caps. Tool-call and LLM-call counters are
/// observational (recorded for the cost record; M.E.AI's MaximumIterationsPerRequest
/// is the hard stop on the loop). Constructed per skill call, used and discarded.
/// </summary>
public sealed class LimitEnforcer
{
    private readonly LoopLimitsConfig _limits;
    private readonly CancellationTokenSource _cts;
    private readonly Stopwatch _stopwatch;

    public LimitEnforcer(LoopLimitsConfig limits, CancellationTokenSource cts)
    {
        _limits = limits;
        _cts = cts;
        _stopwatch = Stopwatch.StartNew();
    }

    public long ElapsedMs => _stopwatch.ElapsedMilliseconds;
    public int ToolCallCount { get; private set; }
    public int LlmCallCount { get; private set; }
    public long AccumulatedInputTokens { get; private set; }
    public long AccumulatedOutputTokens { get; private set; }

    public LimitDecision RecordLlmCall(long inputTokens, long outputTokens)
    {
        LlmCallCount++;
        AccumulatedInputTokens += inputTokens;
        AccumulatedOutputTokens += outputTokens;

        if (AccumulatedInputTokens > _limits.MaxInputTokensPerSkillCall)
            return Cap(LimitDecisionKind.CappedTokens,
                $"input tokens {AccumulatedInputTokens} > {_limits.MaxInputTokensPerSkillCall}");

        if (AccumulatedOutputTokens > _limits.MaxOutputTokensPerSkillCall)
            return Cap(LimitDecisionKind.CappedTokens,
                $"output tokens {AccumulatedOutputTokens} > {_limits.MaxOutputTokensPerSkillCall}");

        return LimitDecision.Continue();
    }

    public void RecordToolCall(string toolName, long durationMs, bool success)
    {
        _ = toolName;
        _ = durationMs;
        _ = success;
        ToolCallCount++;
    }

    public bool CheckTimeLimit()
    {
        if (ElapsedMs <= _limits.MaxSecondsPerSkillCall * 1000L)
            return true;

        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
        return false;
    }

    private LimitDecision Cap(LimitDecisionKind kind, string reason)
    {
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
        return LimitDecision.Cap(kind, reason);
    }
}
