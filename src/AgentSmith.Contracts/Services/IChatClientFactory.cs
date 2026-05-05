using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves a Microsoft.Extensions.AI IChatClient for a given task type.
/// Replaces IAgentProviderFactory + IAgenticAnalyzerFactory + ILlmClientFactory.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Returns the IChatClient configured for the given task type.
    /// Tool-bearing tasks (Primary, Scout, Planning) are wrapped with FunctionInvokingChatClient.
    /// </summary>
    IChatClient Create(TaskType task);

    /// <summary>
    /// Returns the per-task max output tokens (from ConfigBasedModelRegistry).
    /// </summary>
    int GetMaxOutputTokens(TaskType task);

    /// <summary>
    /// Returns the model identifier for the given task type (for logging/cost tracking).
    /// </summary>
    string GetModel(TaskType task);
}
