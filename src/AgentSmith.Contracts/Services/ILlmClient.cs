using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Provider-agnostic LLM completion client.
/// Single prompt in, response with text + token usage out.
/// </summary>
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        TaskType taskType,
        CancellationToken cancellationToken);
}

public sealed record LlmResponse(
    string Text,
    int InputTokens,
    int OutputTokens);
