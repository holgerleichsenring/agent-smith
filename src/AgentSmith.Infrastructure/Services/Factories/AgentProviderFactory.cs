using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

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
            "claude" or "anthropic" => CreateClaude(config),
            "openai" => CreateOpenAi(config),
            "gemini" or "google" => CreateGemini(config),
            "ollama" => CreateOllama(config),
            _ => throw new ConfigurationException(
                $"Unknown agent provider type: '{config.Type}'. Supported: claude, openai, gemini, ollama")
        };
    }

    private ClaudeAgentProvider CreateClaude(AgentConfig config)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        var registry = CreateModelRegistry(config);
        return new ClaudeAgentProvider(
            apiKey, config.Model, config.Retry, config.Cache, config.Compaction,
            registry, config.Pricing, loggerFactory.CreateLogger<ClaudeAgentProvider>());
    }

    private OpenAiAgentProvider CreateOpenAi(AgentConfig config)
    {
        var apiKey = secrets.GetRequired("OPENAI_API_KEY");
        var registry = CreateModelRegistry(config);
        return new OpenAiAgentProvider(
            apiKey, config.Model, config.Retry,
            registry, config.Pricing, loggerFactory.CreateLogger<OpenAiAgentProvider>());
    }

    private GeminiAgentProvider CreateGemini(AgentConfig config)
    {
        var apiKey = secrets.GetRequired("GEMINI_API_KEY");
        var registry = CreateModelRegistry(config);
        return new GeminiAgentProvider(
            apiKey, config.Model,
            registry, config.Pricing, loggerFactory.CreateLogger<GeminiAgentProvider>());
    }

    private OllamaAgentProvider CreateOllama(AgentConfig config)
    {
        var endpoint = config.Endpoint ?? "http://localhost:11434";
        var registry = CreateModelRegistry(config);
        return new OllamaAgentProvider(
            config.Model, endpoint, hasToolCalling: false,
            registry, config.Pricing, loggerFactory.CreateLogger<OllamaAgentProvider>());
    }

    private IModelRegistry? CreateModelRegistry(AgentConfig config)
    {
        if (config.Models is null)
            return null;

        return new ConfigBasedModelRegistry(
            config.Models, loggerFactory.CreateLogger<ConfigBasedModelRegistry>());
    }
}
