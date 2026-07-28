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
/// when an attempt burns the budget without terminal JSON, a finalize turn
/// continues the SAME conversation with tools disabled to demand the JSON
/// from the evidence already gathered (p0385). One fresh retry after that;
/// failure after the retry surfaces to the handler as an exception. JSON
/// decoding is delegated to <see cref="IProjectMapJsonReader"/>.
/// </summary>
public sealed class ProjectAnalyzer(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    IProjectMapJsonReader mapJsonReader,
    IRunContextAccessor runContext,
    ILogger<ProjectAnalyzer> logger) : IProjectAnalyzer
{
    // p0385: single source for the exploration budget — stated in the user prompt
    // AND passed to Create as the FunctionInvokingChatClient iteration cap, so the
    // number the model plans against can't drift from the one the loop enforces.
    private const int ExplorationBudget = 25;

    private const string FinalizeInstruction =
        "Your exploration budget is exhausted. Reply now with ONLY the JSON object "
        + "describing the repository, based on the evidence you gathered so far. "
        + "Omit fields you found no evidence for. No prose, no tool calls.";

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
        var tools = AgenticToolSurface.Scout(fs);
        // p0374: the analyzer is a mechanical read → JSON-ProjectMap task using the
        // SCOUT tool surface — route it to the SCOUT model (a cheap exploration model)
        // instead of PRIMARY (the expensive coding model). Primary here sent 450k+
        // tokens per run through the flagship model at flagship input pricing for
        // work a scout model does fine (with the existing 2-attempt parse retry).
        var chat = chatClientFactory.Create(agent, TaskType.Scout, ExplorationBudget);
        var options = new ChatOptions
        {
            Tools = tools,
            MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Scout),
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
            if (mapJsonReader.TryRead(response.Text ?? string.Empty, out var map, out _))
                return map!;
            var (finalMap, finalError) = await FinalizeAsync(
                chat, options, messages, response, attempt, cancellationToken);
            if (finalMap is not null)
                return finalMap;
            lastError = finalError;
        }

        throw new InvalidOperationException(
            "ProjectAnalyzer failed after 2 attempts (each with a finalize turn): model never " +
            "produced parseable JSON. Check logs for the raw responses; consider adjusting the " +
            "analyzer prompt or upgrading the model.");
    }

    // p0385: the exploration budget ran out (or the reply was prose) — continue the
    // SAME conversation, evidence retained, and demand the JSON with tool use
    // disabled. A shallow map from partial evidence beats a blank-restart retry
    // that deterministically hits the same cap again.
    private async Task<(ProjectMap? Map, string Error)> FinalizeAsync(
        IChatClient chat, ChatOptions options, List<ChatMessage> messages,
        ChatResponse exploration, int attempt, CancellationToken cancellationToken)
    {
        messages.AddRange(exploration.Messages);
        messages.Add(new ChatMessage(ChatRole.User, FinalizeInstruction));
        var response = await chat.GetResponseAsync(
            messages, FinalizeOptions(options), cancellationToken);
        if (mapJsonReader.TryRead(response.Text ?? string.Empty, out var map, out var error))
            return (map, string.Empty);
        logger.LogWarning(
            "ProjectAnalyzer attempt {Attempt} finalize turn still unparseable: {Error}. "
            + "Raw response (truncated): {Raw}",
            attempt, error, Truncate(response.Text));
        return (null, error);
    }

    // Tools stay declared (Anthropic rejects a request whose history contains tool
    // blocks without the tools param) but ToolMode=None forbids further calls.
    private static ChatOptions FinalizeOptions(ChatOptions options) => new()
    {
        Tools = options.Tools,
        ToolMode = ChatToolMode.None,
        MaxOutputTokens = options.MaxOutputTokens,
    };

    private static string ComposePrompt(string userPrompt, int attempt, string lastError) =>
        attempt == 1
            ? userPrompt
            : userPrompt
              + $"\n\nYour previous response could not be parsed as JSON: {lastError}\n"
              + "Respond again with ONLY the JSON object, no surrounding prose, no code fences.";

    private static string Truncate(string? text, int max = 2000) =>
        string.IsNullOrEmpty(text) ? "<empty>"
        : text.Length <= max ? text
        : text[..max] + $"… (+{text.Length - max} more chars)";
}
