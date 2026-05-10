using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves a Microsoft.Extensions.AI IChatClient for a given task type.
/// Replaces IAgentProviderFactory + IAgenticAnalyzerFactory + ILlmClientFactory.
/// AgentConfig is passed per-call (per-pipeline runtime data, not a DI singleton).
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Returns the IChatClient configured for the given agent + task type.
    /// Tool-bearing tasks (Primary, Scout, Planning) are wrapped with FunctionInvokingChatClient.
    /// When <paramref name="maxIterations"/> is non-null, that value is used as the
    /// FunctionInvokingChatClient's MaximumIterationsPerRequest; null preserves the
    /// existing default (25). p0126a additive parameter for per-call cap support.
    /// </summary>
    IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null);

    /// <summary>
    /// Returns the per-task max output tokens (from the agent's ModelRegistryConfig).
    /// </summary>
    int GetMaxOutputTokens(AgentConfig agent, TaskType task);

    /// <summary>
    /// Returns the model identifier for the given agent + task (for logging/cost tracking).
    /// </summary>
    string GetModel(AgentConfig agent, TaskType task);
}
