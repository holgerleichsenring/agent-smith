using AgentSmith.Contracts.Models.Configuration;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for Google Gemini via the GenerativeAI.Microsoft community adapter.
/// </summary>
public sealed class GeminiChatClientBuilder : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "gemini", "google" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = ResolveApiKey(agent)
            ?? throw new InvalidOperationException(
                "GEMINI_API_KEY / GOOGLE_API_KEY (or configured ApiKeySecret) is required for type=gemini.");

        return new GenerativeAIChatClient(apiKey, assignment.Model);
    }

    private static string? ResolveApiKey(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }
        return Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    }
}
