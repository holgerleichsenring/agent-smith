using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for Ollama via OllamaSharp's OllamaApiClient (which natively
/// implements IChatClient). Microsoft.Extensions.AI.Ollama is preview-only and abandoned;
/// OllamaSharp is the de-facto IChatClient-for-Ollama story.
/// </summary>
public sealed class OllamaChatClientBuilder : IChatClientBuilder
{
    private const string DefaultEndpoint = "http://localhost:11434";

    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "ollama" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var endpoint = !string.IsNullOrEmpty(agent.Endpoint) ? agent.Endpoint : DefaultEndpoint;
        return new OllamaApiClient(new Uri(endpoint), assignment.Model);
    }
}
