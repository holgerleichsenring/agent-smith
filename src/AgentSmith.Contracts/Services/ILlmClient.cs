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

    /// <summary>
    /// Optional cache-aware overload. Splits the user message into a stable
    /// prefix (cached on the provider side) and a per-call suffix. Providers
    /// without prompt caching transparently fall back to plain completion.
    /// </summary>
    Task<LlmResponse> CompleteWithCachedPrefixAsync(
        string systemPrompt,
        string userPromptPrefix,
        string userPromptSuffix,
        TaskType taskType,
        CancellationToken cancellationToken)
        => CompleteAsync(
            systemPrompt,
            string.IsNullOrEmpty(userPromptSuffix)
                ? userPromptPrefix
                : $"{userPromptPrefix}\n\n{userPromptSuffix}",
            taskType,
            cancellationToken);
}

public sealed record LlmResponse(
    string Text,
    int InputTokens,
    int OutputTokens,
    string Model = "unknown",
    int CacheCreationTokens = 0,
    int CacheReadTokens = 0);
