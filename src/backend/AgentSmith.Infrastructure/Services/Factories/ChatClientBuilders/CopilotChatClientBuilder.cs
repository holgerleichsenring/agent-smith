using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// 2026-09-07-d5f2: builds an IChatClient for GitHub Copilot, answering on the operator's seat
/// instead of a per-provider API key.
///
/// SupportedTypes is the single name <c>copilot</c>, not an alias pair:
/// ConfigStudioCapabilities.Build hands the dashboard the builders' supported types verbatim and
/// de-duplicates only exact matches, so a second spelling would show up as a second provider in
/// the dropdown for one provider.
/// </summary>
public sealed class CopilotChatClientBuilder(
    ICopilotRuntime runtime,
    ILoggerFactory loggerFactory) : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "copilot" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var seatToken = ResolveSeatToken(agent)
            ?? throw new InvalidOperationException(
                $"A seat token is required for type=copilot. Set the agent's api_key_secret to the "
                + $"name of an environment variable holding it, or set {AgentEnvKeys.CopilotGitHubToken} "
                + $"(or GH_TOKEN / {AgentEnvKeys.GitHubToken}). Copilot rejects org-owned PATs, so the "
                + "token must belong to a person with a seat.");

        var template = new CopilotSessionRequest(
            Model: string.IsNullOrWhiteSpace(assignment.Model) ? agent.Model : assignment.Model,
            ReasoningEffort: null,
            SystemMessage: null,
            SeatToken: seatToken,
            // The call's own tools are declared per session by the client, which knows them;
            // the template opens with none.
            Tools: []);

        return new CopilotSessionChatClient(
            runtime, template, loggerFactory.CreateLogger<CopilotSessionChatClient>());
    }

    /// <summary>
    /// The agent's own secret first — a seat belongs to a person, so two agents naming two
    /// secrets are two seats. The SDK's own precedence is the fallback for a single-seat host.
    /// </summary>
    private static string? ResolveSeatToken(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }
        return Environment.GetEnvironmentVariable(AgentEnvKeys.CopilotGitHubToken)
            ?? Environment.GetEnvironmentVariable("GH_TOKEN")
            ?? Environment.GetEnvironmentVariable(AgentEnvKeys.GitHubToken);
    }
}
