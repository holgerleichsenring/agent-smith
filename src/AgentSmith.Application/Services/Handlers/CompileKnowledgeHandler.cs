using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compiles run history from .agentsmith/runs/ into a wiki knowledge base
/// stored in .agentsmith/wiki/. Tracks last compiled run to avoid reprocessing.
/// </summary>
public sealed class CompileKnowledgeHandler(
    IChatClientFactory chatClientFactory,
    KnowledgePromptBuilder promptBuilder,
    ISandboxFileReaderFactory readerFactory,
    ILogger<CompileKnowledgeHandler> logger)
    : ICommandHandler<CompileKnowledgeContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string WikiDir = "wiki";
    private const string RunsDir = "runs";

    public async Task<CommandResult> ExecuteAsync(
        CompileKnowledgeContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var wikiDir = Path.Combine(agentDir, WikiDir);
        var runsDir = Path.Combine(agentDir, RunsDir);

        var runDirs = await RunDirectoryReader.GetRunDirectoriesAsync(reader, runsDir, cancellationToken);
        if (runDirs.Count == 0)
        {
            logger.LogInformation("No run directories found at {RunsDir}", runsDir);
            return CommandResult.Ok("No runs found, nothing to compile");
        }

        var lastCompiled = context.FullRecompile
            ? 0
            : await RunDirectoryReader.ReadLastCompiledAsync(reader, wikiDir, cancellationToken);
        var newRuns = runDirs
            .Where(r => r.RunNumber > lastCompiled)
            .OrderBy(r => r.RunNumber)
            .ToList();

        if (newRuns.Count == 0)
        {
            logger.LogInformation(
                "Wiki is up to date (last compiled: r{LastCompiled:D2})", lastCompiled);
            return CommandResult.Ok("Wiki up to date");
        }

        var existingWiki = await RunDirectoryReader.ReadExistingWikiAsync(reader, wikiDir, cancellationToken);
        var runData = await RunDirectoryReader.ReadRunDataAsync(reader, newRuns, cancellationToken);

        var systemPrompt = promptBuilder.BuildSystemPrompt();
        var userPrompt = promptBuilder.BuildUserPrompt(existingWiki, runData);

        logger.LogInformation(
            "Compiling {Count} new run(s) into knowledge base (r{From:D2}..r{To:D2})",
            newRuns.Count, newRuns[0].RunNumber, newRuns[^1].RunNumber);

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Summarization);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Summarization);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

        var wikiUpdates = WikiUpdateParser.Parse(response.Text ?? string.Empty, logger);
        if (wikiUpdates.Count == 0)
        {
            logger.LogWarning("LLM returned no wiki updates");
            return CommandResult.Fail("Failed to parse wiki updates from LLM response");
        }

        foreach (var (fileName, content) in wikiUpdates)
        {
            var filePath = Path.Combine(wikiDir, fileName);
            await reader.WriteAsync(filePath, content, cancellationToken);
            logger.LogDebug("Updated wiki file: {File}", fileName);
        }

        var latestRun = newRuns[^1].RunNumber;
        await RunDirectoryReader.WriteLastCompiledAsync(
            reader, wikiDir, latestRun, cancellationToken);

        context.Pipeline.Set(ContextKeys.WikiUpdates, wikiUpdates);

        var summary = $"Wiki updated with {newRuns.Count} run(s), " +
                      $"{wikiUpdates.Count} file(s) written (up to r{latestRun:D2})";
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }
}
