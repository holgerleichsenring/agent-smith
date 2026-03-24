using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates per-project ILlmClient instances based on agent configuration.
/// Mirrors AgentProviderFactory: resolves API key by provider type,
/// builds model registry from project config, applies project retry policy.
/// </summary>
public sealed class LlmClientFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory) : ILlmClientFactory
{
    public ILlmClient Create(AgentConfig config)
    {
        var type = string.IsNullOrEmpty(config.Type) ? "claude" : config.Type;

        return type.ToLowerInvariant() switch
        {
            "claude" or "anthropic" => CreateAnthropic(config),
            "openai" or "ollama" => CreateOpenAiCompatible(config),
            _ => throw new NotSupportedException(
                $"LLM client for provider '{config.Type}' is not yet supported. " +
                $"Supported: claude, anthropic, openai, ollama.")
        };
    }

    private AnthropicLlmClient CreateAnthropic(AgentConfig config)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        var registry = CreateModelRegistry(config);
        var logger = loggerFactory.CreateLogger<AnthropicLlmClient>();
        return new AnthropicLlmClient(apiKey, config.Retry, registry, logger);
    }

    private OpenAiLlmClient CreateOpenAiCompatible(AgentConfig config)
    {
        var secretName = config.ApiKeySecret ?? "OPENAI_API_KEY";
        var apiKey = secrets.GetOptional(secretName);
        var endpoint = config.Endpoint ?? "https://api.openai.com";
        var client = new OpenAiCompatibleClient(
            endpoint + "/v1", apiKey, loggerFactory.CreateLogger("OpenAiLlmClient"));
        var registry = CreateModelRegistry(config);
        return new OpenAiLlmClient(
            client, config.Model, registry, loggerFactory.CreateLogger<OpenAiLlmClient>());
    }

    private IModelRegistry CreateModelRegistry(AgentConfig config)
    {
        var registryConfig = config.Models ?? new ModelRegistryConfig();
        return new ConfigBasedModelRegistry(
            registryConfig, loggerFactory.CreateLogger<ConfigBasedModelRegistry>());
    }
}
