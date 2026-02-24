using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Provider-agnostic LLM completion client.
/// Single prompt in, text out. Model routing via TaskType.
/// </summary>
public interface ILlmClient
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        TaskType taskType,
        CancellationToken cancellationToken);
}
