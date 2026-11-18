using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for Google Gemini via the GenerativeAI.Microsoft community adapter.
/// </summary>
public sealed class GeminiChatClientBuilder(ICredentialResolver credentials) : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "gemini", "google" };

    // The list files the bare Gemini API ids under this provider; its "gemini" ids carry a "gemini/" prefix.
    public IReadOnlyList<string> PriceListProviders { get; } = ["vertex_ai-language-models"];

    public bool AcceptsImageAfterToolResult => true;

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = new AgentApiKey(credentials).Resolve(agent, AgentEnvKeys.GeminiApiKey, GoogleApiKey)
            ?? throw new InvalidOperationException(
                "GEMINI_API_KEY / GOOGLE_API_KEY (or configured ApiKeySecret) is required for type=gemini.");

        // 2026-11-10-e706: bounded by the agent's network timeout like every other provider.
        return new NetworkTimeoutChatClient(
            new GenerativeAIChatClient(apiKey, assignment.Model),
            OpenAiChatClientBuilder.ResolveNetworkTimeout(agent));
    }

    /// <summary>The Google AI SDKs' second name for the Gemini key.</summary>
    public const string GoogleApiKey = "GOOGLE_API_KEY";
}
