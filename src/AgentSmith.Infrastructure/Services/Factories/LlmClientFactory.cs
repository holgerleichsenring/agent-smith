using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates per-project ILlmClient instances based on agent configuration.
/// Provider creators are registered in a dictionary — no switch statement.
/// </summary>
public sealed class LlmClientFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory) : ILlmClientFactory
{
    private readonly Dictionary<string, Func<AgentConfig, ILlmClient>> _creators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = config => CreateAnthropic(config, secrets, loggerFactory),
        ["anthropic"] = config => CreateAnthropic(config, secrets, loggerFactory),
        ["openai"] = config => CreateOpenAiCompatible(config, secrets, loggerFactory),
        ["azure-openai"] = config => CreateAzureOpenAiCompatible(config, secrets, loggerFactory),
        ["azure"] = config => CreateAzureOpenAiCompatible(config, secrets, loggerFactory),
        ["ollama"] = config => CreateOpenAiCompatible(config, secrets, loggerFactory),
    };

    public ILlmClient Create(AgentConfig config)
    {
        var type = string.IsNullOrEmpty(config.Type) ? "claude" : config.Type;

        if (_creators.TryGetValue(type, out var creator))
            return creator(config);

        throw new NotSupportedException(
            $"LLM client for provider '{config.Type}' is not yet supported. " +
            $"Supported: {string.Join(", ", _creators.Keys)}");
    }

    private static AnthropicLlmClient CreateAnthropic(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        var registry = CreateModelRegistry(config, loggerFactory);
        return new AnthropicLlmClient(apiKey, config.Retry, registry,
            loggerFactory.CreateLogger<AnthropicLlmClient>());
    }

    private static OpenAiLlmClient CreateOpenAiCompatible(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var secretName = config.ApiKeySecret ?? "OPENAI_API_KEY";
        var apiKey = secrets.GetOptional(secretName);
        var endpoint = config.Endpoint ?? "https://api.openai.com";
        var client = new OpenAiCompatibleClient(
            endpoint + "/v1", apiKey, loggerFactory.CreateLogger("OpenAiLlmClient"));
        var registry = CreateModelRegistry(config, loggerFactory);
        return new OpenAiLlmClient(client, registry,
            loggerFactory.CreateLogger<OpenAiLlmClient>());
    }

    private static AzureOpenAiLlmClient CreateAzureOpenAiCompatible(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AzureOpenAiSetup");
        var secretName = config.ApiKeySecret ?? "AZURE_OPENAI_API_KEY";
        var apiKey = secrets.GetRequired(secretName);
        var endpoint = config.Endpoint
                       ?? throw new NotSupportedException("Azure OpenAI requires 'endpoint' in agent config");
        var defaultDeployment = config.Deployment ?? "";
        var apiVersion = config.ApiVersion ?? "2025-01-01-preview";

        logger.LogInformation(
            "Azure OpenAI: endpoint={Endpoint}, defaultDeployment={Deployment}, apiVersion={Version}, key={KeyLength}chars",
            endpoint, defaultDeployment, apiVersion, apiKey.Length);

        var registry = CreateModelRegistry(config, loggerFactory);
        return new AzureOpenAiLlmClient(
            endpoint, apiKey, defaultDeployment, apiVersion, registry,
            loggerFactory.CreateLogger<AzureOpenAiLlmClient>());
    }

    private static IModelRegistry CreateModelRegistry(AgentConfig config, ILoggerFactory loggerFactory)
    {
        var registryConfig = config.Models ?? new ModelRegistryConfig();
        return new ConfigBasedModelRegistry(
            registryConfig, loggerFactory.CreateLogger<ConfigBasedModelRegistry>());
    }
}
