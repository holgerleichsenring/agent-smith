using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates IAgenticAnalyzer instances per AgentConfig. Mirrors AgentProviderFactory:
/// type-string dispatch via a creators dictionary. Ollama is explicitly NOT supported
/// in this phase (p0110a) — its tool-use is model-dependent and inconsistent;
/// throws NotSupportedException with operator-facing guidance.
/// </summary>
public sealed class AgenticAnalyzerFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory) : IAgenticAnalyzerFactory
{
    private readonly Dictionary<string, Func<AgentConfig, IAgenticAnalyzer>> _creators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude"] = config => CreateClaude(config, secrets, loggerFactory),
            ["anthropic"] = config => CreateClaude(config, secrets, loggerFactory),
            ["openai"] = config => CreateOpenAi(config, secrets, loggerFactory),
            ["azure-openai"] = config => CreateAzureOpenAi(config, secrets, loggerFactory),
            ["azure"] = config => CreateAzureOpenAi(config, secrets, loggerFactory),
            ["gemini"] = config => CreateGemini(config, secrets, loggerFactory),
            ["google"] = config => CreateGemini(config, secrets, loggerFactory),
        };

    public IAgenticAnalyzer Create(AgentConfig config)
    {
        var type = config.Type ?? "claude";

        if (_creators.TryGetValue(type, out var creator))
            return creator(config);

        if (string.Equals(type, "ollama", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Ollama agent does not support IAgenticAnalyzer in p0110a. " +
                "Tool-use stability varies by underlying model. Use Claude/OpenAI/Azure-OpenAI/Gemini " +
                "as the agent type for tool-driven analysis. " +
                "See docs/configuration/agents.md for the per-provider capability matrix.");

        throw new ConfigurationException(
            $"Unknown agent provider type: '{type}'. Supported: {string.Join(", ", _creators.Keys)}");
    }

    private static ClaudeAgenticAnalyzer CreateClaude(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        var maxTokens = ResolveMaxTokens(config);
        return new ClaudeAgenticAnalyzer(
            apiKey, config.Model, maxTokens, config.Retry,
            loggerFactory.CreateLogger<ClaudeAgenticAnalyzer>());
    }

    private static OpenAiAgenticAnalyzer CreateOpenAi(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var secretName = config.ApiKeySecret ?? "OPENAI_API_KEY";
        var apiKey = secrets.GetRequired(secretName);
        var endpoint = config.Endpoint is not null ? new Uri(config.Endpoint) : null;
        return new OpenAiAgenticAnalyzer(
            apiKey, config.Model, endpoint, config.Retry,
            loggerFactory.CreateLogger<OpenAiAgenticAnalyzer>());
    }

    private static AzureOpenAiAgenticAnalyzer CreateAzureOpenAi(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var secretName = config.ApiKeySecret ?? "AZURE_OPENAI_API_KEY";
        var apiKey = secrets.GetRequired(secretName);
        var endpoint = config.Endpoint
            ?? throw new ConfigurationException("Azure OpenAI requires 'endpoint' in agent config");
        var deployment = ResolveAzureDeployment(config)
            ?? throw new ConfigurationException(
                "Azure OpenAI requires a deployment name. Set agent.deployment in agentsmith.yml, " +
                "OR set agent.models.Planning.deployment (the analyzer falls back to the Planning task's deployment).");
        return new AzureOpenAiAgenticAnalyzer(
            apiKey, new Uri(endpoint), deployment, config.Retry,
            loggerFactory.CreateLogger<AzureOpenAiAgenticAnalyzer>());
    }

    private static string? ResolveAzureDeployment(AgentConfig config)
    {
        // The analyzer is plan-like: prefer the Planning task's deployment if set.
        // Falls back to the agent-wide deployment, then to the agent-wide model name.
        if (!string.IsNullOrWhiteSpace(config.Models?.Planning?.Deployment))
            return config.Models.Planning.Deployment;
        if (!string.IsNullOrWhiteSpace(config.Deployment))
            return config.Deployment;
        return string.IsNullOrWhiteSpace(config.Model) ? null : config.Model;
    }

    private static GeminiAgenticAnalyzer CreateGemini(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var apiKey = secrets.GetRequired("GEMINI_API_KEY");
        return new GeminiAgenticAnalyzer(
            apiKey, config.Model,
            loggerFactory.CreateLogger<GeminiAgenticAnalyzer>());
    }

    private static int ResolveMaxTokens(AgentConfig config)
    {
        // Default for analyzer runs; per-task overrides aren't wired here yet — that lands
        // in p0110b when the Analyzer task type joins the IModelRegistry mapping.
        return AgentDefaults.DefaultMaxTokens;
    }
}
