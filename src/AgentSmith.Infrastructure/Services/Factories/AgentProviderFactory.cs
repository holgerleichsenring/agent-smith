using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate IAgentProvider based on configuration type.
/// Provider creators are registered in a dictionary — no switch statement.
/// </summary>
public sealed class AgentProviderFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory,
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail) : IAgentProviderFactory
{
    private readonly Dictionary<string, Func<AgentConfig, IAgentProvider>> _creators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = config => CreateClaude(config, secrets, loggerFactory, dialogueTransport, dialogueTrail),
        ["anthropic"] = config => CreateClaude(config, secrets, loggerFactory, dialogueTransport, dialogueTrail),
        ["openai"] = config => CreateOpenAi(config, secrets, loggerFactory),
        ["gemini"] = config => CreateGemini(config, secrets, loggerFactory),
        ["google"] = config => CreateGemini(config, secrets, loggerFactory),
        ["ollama"] = config => CreateOllama(config, loggerFactory),
    };

    public IAgentProvider Create(AgentConfig config)
    {
        var type = config.Type ?? "claude";

        if (_creators.TryGetValue(type, out var creator))
            return creator(config);

        throw new ConfigurationException(
            $"Unknown agent provider type: '{type}'. Supported: {string.Join(", ", _creators.Keys)}");
    }

    private static ClaudeAgentProvider CreateClaude(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory,
        IDialogueTransport dialogueTransport, IDialogueTrail dialogueTrail)
    {
        var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
        var registry = CreateModelRegistry(config, loggerFactory);
        return new ClaudeAgentProvider(
            apiKey, config.Model, config.Retry, config.Cache, config.Compaction,
            registry, config.Pricing, loggerFactory.CreateLogger<ClaudeAgentProvider>(),
            dialogueTransport, dialogueTrail);
    }

    private static OpenAiAgentProvider CreateOpenAi(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var secretName = config.ApiKeySecret ?? "OPENAI_API_KEY";
        var apiKey = secrets.GetRequired(secretName);
        var registry = CreateModelRegistry(config, loggerFactory);
        var endpoint = config.Endpoint is not null ? new Uri(config.Endpoint) : null;
        return new OpenAiAgentProvider(
            apiKey, config.Model, config.Retry,
            registry, config.Pricing, loggerFactory.CreateLogger<OpenAiAgentProvider>(), endpoint);
    }

    private static GeminiAgentProvider CreateGemini(
        AgentConfig config, SecretsProvider secrets, ILoggerFactory loggerFactory)
    {
        var apiKey = secrets.GetRequired("GEMINI_API_KEY");
        var registry = CreateModelRegistry(config, loggerFactory);
        return new GeminiAgentProvider(
            apiKey, config.Model,
            registry, config.Pricing, loggerFactory.CreateLogger<GeminiAgentProvider>());
    }

    private static OllamaAgentProvider CreateOllama(
        AgentConfig config, ILoggerFactory loggerFactory)
    {
        var endpoint = config.Endpoint ?? "http://localhost:11434";
        var ollamaLogger = loggerFactory.CreateLogger<OllamaAgentProvider>();
        var client = new OpenAiCompatibleClient(
            endpoint + "/v1", null, loggerFactory.CreateLogger("Ollama"));

        var hasToolCalling = CheckOllamaCapabilities(client, config.Model, endpoint, ollamaLogger);
        var registry = CreateModelRegistry(config, loggerFactory);

        return new OllamaAgentProvider(
            config.Model, client, hasToolCalling,
            registry, ollamaLogger);
    }

    private static bool CheckOllamaCapabilities(
        OpenAiCompatibleClient client, string model, string endpoint, ILogger logger)
    {
        try
        {
            var version = client.GetVersionAsync(CancellationToken.None).GetAwaiter().GetResult();
            logger.LogInformation("Connected to Ollama {Version} at {Endpoint}", version, endpoint);

            var hasTools = client.CheckToolCallingSupport(model, CancellationToken.None).GetAwaiter().GetResult();
            logger.LogInformation("Model {Model}: tool_calling={HasTools}", model, hasTools);
            return hasTools;
        }
        catch (HttpRequestException ex)
        {
            throw new ConfigurationException(
                $"Cannot connect to Ollama at '{endpoint}'. " +
                $"Ensure Ollama is running: docker run -d -p 11434:11434 ollama/ollama && " +
                $"docker exec ollama ollama pull {model}. Error: {ex.Message}");
        }
    }

    private static IModelRegistry? CreateModelRegistry(AgentConfig config, ILoggerFactory loggerFactory)
    {
        if (config.Models is null)
            return null;

        return new ConfigBasedModelRegistry(
            config.Models, loggerFactory.CreateLogger<ConfigBasedModelRegistry>());
    }
}
