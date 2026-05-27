using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compiles a multi-agent discussion into a consolidated report.
/// No repository needed — stores the result in PipelineContext for DeliverOutput.
/// Used by api-security-scan and other repo-less pipelines.
/// </summary>
public sealed class CompileFindingsHandler(
    ILogger<CompileFindingsHandler> logger)
    : ICommandHandler<CompileFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        CompileFindingsContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var log) || log is null || log.Count == 0)
        {
            logger.LogInformation("No discussion log to compile, skipping");
            return Task.FromResult(CommandResult.Ok("No discussion log, skipping compilation"));
        }

        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var summary);

        var markdown = FormatFindings(log, summary);

        context.Pipeline.Set(ContextKeys.ConsolidatedPlan, markdown);

        logger.LogInformation(
            "Compiled findings from {Entries} discussion entries ({Chars} chars)",
            log.Count, markdown.Length);

        return Task.FromResult(
            CommandResult.Ok($"Findings compiled ({log.Count} entries, {markdown.Length} chars)"));
    }

    private static string FormatFindings(List<DiscussionEntry> log, string? summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Security Scan Results");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"**Participants:** {string.Join(", ", log.Select(e => e.DisplayName).Distinct())}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine(summary);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Analysis");
        sb.AppendLine();

        var currentRound = -1;
        foreach (var entry in log)
        {
            if (entry.Round != currentRound)
            {
                currentRound = entry.Round;
                sb.AppendLine($"### Round {currentRound}");
                sb.AppendLine();
            }

            sb.AppendLine($"#### {entry.Emoji} {entry.DisplayName}");
            sb.AppendLine();
            sb.AppendLine(entry.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
