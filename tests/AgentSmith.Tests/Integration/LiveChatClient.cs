using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-17-274cb: the live-LLM chat client the LiveLLM evals share, kept when the skill-call
/// runtime probe that carried it went with the runtime.
/// </summary>
internal static class LiveChatClient
{
    /// <summary>
    /// Build an IChatClient against whatever provider is configured in env. Tries
    /// Azure OpenAI first (the user's primary), falls back to public OpenAI.
    /// </summary>
    public static IChatClient Build()
    {
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (!string.IsNullOrWhiteSpace(azureKey) &&
            !string.IsNullOrWhiteSpace(azureEndpoint) &&
            !string.IsNullOrWhiteSpace(azureDeployment))
        {
            var agent = new AgentConfig
            {
                Type = "azure_openai",
                Endpoint = azureEndpoint,
                Deployment = azureDeployment
            };
            var assignment = new ModelAssignment
            {
                Model = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL") ?? "gpt-4.1",
                Deployment = azureDeployment
            };
            return new OpenAiChatClientBuilder().Build(agent, assignment);
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            var agent = new AgentConfig { Type = "openai" };
            var assignment = new ModelAssignment { Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1" };
            return new OpenAiChatClientBuilder().Build(agent, assignment);
        }

        throw new InvalidOperationException(
            "No LLM credentials in env. Set either:\n" +
            "  AZURE_OPENAI_API_KEY + AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_DEPLOYMENT (+ optional AZURE_OPENAI_MODEL)\n" +
            "  or OPENAI_API_KEY (+ optional OPENAI_MODEL, default gpt-4.1)\n" +
            "Then re-run.");
    }
}
