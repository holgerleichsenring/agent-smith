using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Drives an <see cref="IChatClient"/> through the read-only scout tool
/// subset (ReadFile + Grep + ListFiles) and parses the model's terminal JSON
/// into a <see cref="ProjectMap"/>. One retry on JSON-parse failure with the
/// parse error appended to the user prompt; failure after the retry surfaces
/// to the handler as an exception. JSON decoding is delegated to
/// <see cref="IProjectMapJsonReader"/>.
/// </summary>
public sealed class ProjectAnalyzer(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    IProjectMapJsonReader mapJsonReader,
    IRunContextAccessor runContext,
    ILogger<ProjectAnalyzer> logger) : IProjectAnalyzer
{
    public async Task<ProjectMap> AnalyzeAsync(
        string repositoryPath, AgentConfig agent, ISandbox sandbox,
        CancellationToken cancellationToken, string? repoName = null)
    {
        var systemPrompt = prompts.Get("project-analyzer-system");
        var userPrompt = $"Repository to analyze: {repositoryPath}\n\nStart by listing the root directory.";
        var fs = new FilesystemToolHost(sandbox, repositoryPath);
        var tools = AgenticToolSurface.Scout(fs);
        var chat = chatClientFactory.Create(agent, TaskType.Primary);
        var options = new ChatOptions
        {
            Tools = tools,
            MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Primary),
        };

        var lastError = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, ComposePrompt(userPrompt, attempt, lastError)),
            };
            using var _scope = runContext.BeginCallScope(
                "project-analyzer", "BootstrapDiscover", repoName);
            var response = await chat.GetResponseAsync(messages, options, cancellationToken);
            logger.LogInformation(
                "ProjectAnalyzer attempt {Attempt}: {In}+{Out} tokens",
                attempt, response.Usage?.InputTokenCount ?? 0, response.Usage?.OutputTokenCount ?? 0);
            if (mapJsonReader.TryRead(response.Text ?? string.Empty, out var map, out var parseError))
                return map!;
            lastError = parseError;
            logger.LogWarning(
                "ProjectAnalyzer attempt {Attempt} produced unparseable output: {Error}", attempt, parseError);
        }

        throw new InvalidOperationException(
            "ProjectAnalyzer failed after 2 attempts: model never produced parseable JSON. " +
            "Check logs for the raw responses; consider adjusting the analyzer prompt or upgrading the model.");
    }

    private static string ComposePrompt(string userPrompt, int attempt, string lastError) =>
        attempt == 1
            ? userPrompt
            : userPrompt
              + $"\n\nYour previous response could not be parsed as JSON: {lastError}\n"
              + "Respond again with ONLY the JSON object, no surrounding prose, no code fences.";
}
