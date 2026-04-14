using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads the last N runs from .agentsmith/runs/ and wiki summaries from .agentsmith/wiki/.
/// Compiles run history into a single text block stored in PipelineContext.
/// </summary>
public sealed class LoadRunsHandler(
    ILogger<LoadRunsHandler> logger)
    : ICommandHandler<LoadRunsContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string RunsDir = "runs";
    private const string WikiDir = "wiki";

    public async Task<CommandResult> ExecuteAsync(
        LoadRunsContext context, CancellationToken cancellationToken)
    {
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runsDir = Path.Combine(agentDir, RunsDir);

        var sb = new StringBuilder();
        var runCount = 0;

        if (Directory.Exists(runsDir))
        {
            var runDirs = RunDirectoryReader.GetRunDirectories(runsDir);
            var recentRuns = runDirs
                .OrderByDescending(r => r.RunNumber)
                .Take(context.LookbackRuns)
                .OrderBy(r => r.RunNumber)
                .ToList();

            foreach (var run in recentRuns)
            {
                var resultPath = Path.Combine(run.Path, "result.md");
                if (!File.Exists(resultPath))
                    continue;

                var content = await File.ReadAllTextAsync(resultPath, cancellationToken);
                sb.AppendLine($"### Run r{run.RunNumber:D2} ({run.Name})");
                sb.AppendLine(content);
                sb.AppendLine();
                runCount++;
            }
        }

        // Also load wiki summaries if available
        var wikiDir = Path.Combine(agentDir, WikiDir);
        if (Directory.Exists(wikiDir))
        {
            var wikiFiles = Directory.GetFiles(wikiDir, "*.md");
            if (wikiFiles.Length > 0)
            {
                sb.AppendLine("## Wiki Knowledge Base");
                sb.AppendLine();
                foreach (var wikiFile in wikiFiles.OrderBy(f => f))
                {
                    var content = await File.ReadAllTextAsync(wikiFile, cancellationToken);
                    sb.AppendLine($"### {Path.GetFileNameWithoutExtension(wikiFile)}");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }
        }

        var history = sb.ToString();

        if (string.IsNullOrWhiteSpace(history))
        {
            logger.LogInformation("No run history or wiki found");
            return CommandResult.Ok("No run history found");
        }

        context.Pipeline.Set(ContextKeys.RunHistory, history);

        logger.LogInformation("Loaded {Count} recent run(s)", runCount);
        return CommandResult.Ok($"Loaded {runCount} recent run(s)");
    }
}
