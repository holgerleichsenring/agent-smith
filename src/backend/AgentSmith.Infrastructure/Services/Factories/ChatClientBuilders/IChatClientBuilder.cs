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
    /// AgentConfig.Type values this builder handles (e.g. "claude", "openai", "azure_openai").
    /// </summary>
    IReadOnlyList<string> SupportedTypes { get; }

    /// <summary>
    /// 2026-10-01-283dd: whether the built client delivers an image placed in a user message
    /// right after a tool result. A transport fact, not the agent's vision flag: vision says
    /// the model can see, this says the picture arrives.
    /// </summary>
    bool AcceptsImageAfterToolResult { get; }

    /// <summary>
    /// Builds the bare IChatClient for the given agent + task assignment.
    /// FunctionInvokingChatClient wrapping is the factory's responsibility.
    /// </summary>
    IChatClient Build(AgentConfig agent, ModelAssignment assignment);
}
