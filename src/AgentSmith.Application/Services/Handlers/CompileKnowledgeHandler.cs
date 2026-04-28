using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compiles run history from .agentsmith/runs/ into a wiki knowledge base
/// stored in .agentsmith/wiki/. Tracks last compiled run to avoid reprocessing.
/// </summary>
public sealed class CompileKnowledgeHandler(
    ILlmClient llmClient,
    KnowledgePromptBuilder promptBuilder,
    ILogger<CompileKnowledgeHandler> logger)
    : ICommandHandler<CompileKnowledgeContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string WikiDir = "wiki";
    private const string RunsDir = "runs";

    public async Task<CommandResult> ExecuteAsync(
        CompileKnowledgeContext context, CancellationToken cancellationToken)
    {
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var wikiDir = Path.Combine(agentDir, WikiDir);
        var runsDir = Path.Combine(agentDir, RunsDir);

        if (!Directory.Exists(runsDir))
        {
            logger.LogInformation("No runs directory found at {RunsDir}", runsDir);
            return CommandResult.Ok("No runs directory found, nothing to compile");
        }

        var runDirs = RunDirectoryReader.GetRunDirectories(runsDir);
        if (runDirs.Count == 0)
        {
            logger.LogInformation("No run directories found");
            return CommandResult.Ok("No runs found, nothing to compile");
        }

        var lastCompiled = context.FullRecompile
            ? 0
            : RunDirectoryReader.ReadLastCompiled(wikiDir);
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

        Directory.CreateDirectory(wikiDir);

        var existingWiki = RunDirectoryReader.ReadExistingWiki(wikiDir);
        var runData = await RunDirectoryReader.ReadRunDataAsync(newRuns, cancellationToken);

        var systemPrompt = promptBuilder.BuildSystemPrompt();
        var userPrompt = promptBuilder.BuildUserPrompt(existingWiki, runData);

        logger.LogInformation(
            "Compiling {Count} new run(s) into knowledge base (r{From:D2}..r{To:D2})",
            newRuns.Count, newRuns[0].RunNumber, newRuns[^1].RunNumber);

        var response = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Summarization, cancellationToken);

        var wikiUpdates = WikiUpdateParser.Parse(response.Text, logger);
        if (wikiUpdates.Count == 0)
        {
            logger.LogWarning("LLM returned no wiki updates");
            return CommandResult.Fail("Failed to parse wiki updates from LLM response");
        }

        foreach (var (fileName, content) in wikiUpdates)
        {
            var filePath = Path.Combine(wikiDir, fileName);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogDebug("Updated wiki file: {File}", fileName);
        }

        var latestRun = newRuns[^1].RunNumber;
        await RunDirectoryReader.WriteLastCompiledAsync(
            wikiDir, latestRun, cancellationToken);

        context.Pipeline.Set(ContextKeys.WikiUpdates, wikiUpdates);

        var summary = $"Wiki updated with {newRuns.Count} run(s), " +
                      $"{wikiUpdates.Count} file(s) written (up to r{latestRun:D2})";
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }
}
