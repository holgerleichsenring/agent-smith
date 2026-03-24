using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Accumulates LLM token usage across all pipeline steps.
/// Stored in PipelineContext, read by output handlers at the end.
/// </summary>
public sealed class PipelineCostTracker
{
    private int _totalInputTokens;
    private int _totalOutputTokens;
    private int _callCount;

    public int TotalInputTokens => _totalInputTokens;
    public int TotalOutputTokens => _totalOutputTokens;
    public int CallCount => _callCount;

    public void Track(LlmResponse response)
    {
        Interlocked.Add(ref _totalInputTokens, response.InputTokens);
        Interlocked.Add(ref _totalOutputTokens, response.OutputTokens);
        Interlocked.Increment(ref _callCount);
    }

    public override string ToString() =>
        $"{CallCount} LLM calls · {TotalInputTokens + TotalOutputTokens} tokens ({TotalInputTokens} in, {TotalOutputTokens} out)";

    public string FormatSummary() => ToString();

    public static PipelineCostTracker GetOrCreate(PipelineContext pipeline)
    {
        const string key = "PipelineCostTracker";
        if (pipeline.TryGet<PipelineCostTracker>(key, out var existing)
            && existing is not null)
            return existing;

        var tracker = new PipelineCostTracker();
        pipeline.Set(key, tracker);
        return tracker;
    }
}
