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
/// into a <see cref="ProjectMap"/>. The prompt states the tool-call budget;
/// when an attempt burns the budget without terminal JSON,
/// <see cref="IProjectMapFinalizer"/> continues the SAME conversation with tools
/// disabled to demand the JSON from the evidence already gathered (p0385). One
/// fresh retry after that; failure after the retry surfaces to the handler as an
/// exception. JSON decoding is delegated to <see cref="IProjectMapJsonReader"/>.
/// </summary>
public sealed class ProjectAnalyzer(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    IProjectMapJsonReader mapJsonReader,
    IProjectMapFinalizer finalizer,
    IRunContextAccessor runContext,
    AgenticToolSurface toolSurface,
    ILogger<ProjectAnalyzer> logger) : IProjectAnalyzer
{
    // p0385: single source for the exploration budget — stated in the user prompt
    // AND passed to Create as the FunctionInvokingChatClient iteration cap, so the
    // number the model plans against can't drift from the one the loop enforces.
    private const int ExplorationBudget = 25;

    public async Task<ProjectMap> AnalyzeAsync(
        string repositoryPath, AgentConfig agent, ISandbox sandbox,
        CancellationToken cancellationToken, string? repoName = null)
    {
        var systemPrompt = prompts.Get("project-analyzer-system");
        var userPrompt =
            $"Repository to analyze: {repositoryPath}\n\n"
            + $"You have a budget of {ExplorationBudget} tool calls for exploration. "
            + "Reserve your final reply for the JSON object only.\n\n"
            + "Start by listing the root directory.";
        var fs = new FilesystemToolHost(sandbox, repositoryPath);
        var tools = toolSurface.Scout(fs);
        var options = new ChatOptions
        {
            Tools = tools,
            MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Scout),
        };

        var lastError = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            // p0374: a mechanical read → JSON-ProjectMap task on the SCOUT tool surface
            // routes to the SCOUT model, not PRIMARY — this sent 450k+ tokens per run
            // through the flagship model at flagship input pricing.
            // 2026-08-27-3eb1: a client PER ATTEMPT, and the agent's compaction settings
            // handed to it. The whole sweep is ONE GetResponseAsync in which every tool
            // result is appended to one message list, so it needs the same in-flight
            // reduction the coding master gets; and the reducer's fold watermark is an
            // absolute index, so a client reused across attempts would meet attempt 2's
            // two-message list holding attempt 1's watermark and summary.
            var chat = chatClientFactory.Create(
                agent, TaskType.Scout, ExplorationBudget, masterLoopHooks: null, agent.Compaction);
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
            if (mapJsonReader.TryRead(response.Text ?? string.Empty, out var map, out _))
                return map!;
            var (finalMap, finalError) = await finalizer.FinalizeAsync(
                chat, options, messages, response, attempt, repoName, cancellationToken);
            if (finalMap is not null)
                return finalMap;
            lastError = finalError;
        }

        throw new InvalidOperationException(
            "ProjectAnalyzer failed after 2 attempts (each with a finalize turn): model never " +
            "produced parseable JSON. Check logs for the raw responses; consider adjusting the " +
            "analyzer prompt or upgrading the model.");
    }

    private static string ComposePrompt(string userPrompt, int attempt, string lastError) =>
        attempt == 1
            ? userPrompt
            : userPrompt
              + $"\n\nYour previous response could not be parsed as JSON: {lastError}\n"
              + "Respond again with ONLY the JSON object, no surrounding prose, no code fences.";
}
