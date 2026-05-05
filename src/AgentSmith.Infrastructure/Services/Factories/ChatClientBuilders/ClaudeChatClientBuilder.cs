using AgentSmith.Contracts.Models.Configuration;
using Anthropic.SDK;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient against Anthropic Claude via tghamm/Anthropic.SDK.
/// AnthropicClient.Messages implements IChatClient natively (5.10.0+).
/// </summary>
public sealed class ClaudeChatClientBuilder : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "claude", "anthropic" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = ResolveApiKey(agent)
            ?? throw new InvalidOperationException(
                "ANTHROPIC_API_KEY (or configured ApiKeySecret) is required for type=claude.");

        var anthropic = new AnthropicClient(apiKey);
        return anthropic.Messages;
    }

    private static string? ResolveApiKey(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }
        return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }
}
