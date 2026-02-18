using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Configuration;
using AgentSmith.Infrastructure.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Factories;

/// <summary>
/// Creates the appropriate IAgentProvider based on configuration type.
/// </summary>
public sealed class AgentProviderFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory) : IAgentProviderFactory
{
    public IAgentProvider Create(AgentConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "claude" => CreateClaude(config),
            "openai" => throw new NotSupportedException("OpenAI provider not yet implemented."),
            _ => throw new ConfigurationException($"Unknown agent provider type: {config.Type}")
        };
    }

    private ClaudeAgentProvider CreateClaude(AgentConfig config)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        return new ClaudeAgentProvider(
            apiKey, config.Model, config.Retry, config.Cache, config.Compaction,
            loggerFactory.CreateLogger<ClaudeAgentProvider>());
    }
}
