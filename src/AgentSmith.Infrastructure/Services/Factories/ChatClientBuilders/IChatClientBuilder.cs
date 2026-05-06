using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Provider-specific builder that produces a configured Microsoft.Extensions.AI
/// IChatClient from an AgentConfig + per-task ModelAssignment.
/// </summary>
public interface IChatClientBuilder
{
    /// <summary>
    /// AgentConfig.Type values this builder handles (e.g. "claude", "openai", "azure-openai").
    /// </summary>
    IReadOnlyList<string> SupportedTypes { get; }

    /// <summary>
    /// Builds the bare IChatClient for the given agent + task assignment.
    /// FunctionInvokingChatClient wrapping is the factory's responsibility.
    /// </summary>
    IChatClient Build(AgentConfig agent, ModelAssignment assignment);
}
