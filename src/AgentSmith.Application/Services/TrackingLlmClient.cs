using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Decorator that tracks token usage for every LLM call in the PipelineCostTracker.
/// Wraps any ILlmClient without changing its behavior.
/// </summary>
public sealed class TrackingLlmClient(ILlmClient inner, PipelineCostTracker tracker) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt, string userPrompt,
        TaskType taskType, CancellationToken cancellationToken)
    {
        var response = await inner.CompleteAsync(systemPrompt, userPrompt, taskType, cancellationToken);
        tracker.Track(response);
        return response;
    }
}
