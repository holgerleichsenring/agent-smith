using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compiles the multi-agent discussion log into a markdown document,
/// writes it to the repository, and sets CodeChanges for CommitAndPR.
/// </summary>
public sealed class CompileDiscussionHandler(
    ILogger<CompileDiscussionHandler> logger)
    : ICommandHandler<CompileDiscussionContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CompileDiscussionContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var log) || log is null || log.Count == 0)
        {
            return CommandResult.Fail("No discussion log to compile");
        }

        var ticket = context.Pipeline.Get<Ticket>(ContextKeys.Ticket);
        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var summary);

        var markdown = FormatDiscussion(ticket, log, summary);

        var fileName = $"discussion-{ticket.Id}.md";
        var filePath = Path.Combine(context.Repository.LocalPath, fileName);
        await File.WriteAllTextAsync(filePath, markdown, cancellationToken);

        var changes = new List<CodeChange>
        {
            new(new FilePath(fileName), markdown, "add")
        };
        context.Pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)changes);

        // Set a minimal Plan so CommitAndPR can generate a PR description
        var plan = new Plan(ticket.Title, [], summary ?? "Discussion compiled");
        context.Pipeline.Set(ContextKeys.Plan, plan);

        logger.LogInformation(
            "Compiled discussion with {Entries} entries into {File}",
            log.Count, fileName);

        return CommandResult.Ok($"Discussion compiled into {fileName} ({markdown.Length} chars)");
    }

    internal static string FormatDiscussion(
        Ticket ticket, List<DiscussionEntry> log, string? summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Ticket:** #{ticket.Id}");
        sb.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"**Participants:** {string.Join(", ", log.Select(e => e.DisplayName).Distinct())}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine(summary);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Discussion");
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

            if (entry.Content.Trim() == "[SILENCE]")
            {
                sb.AppendLine($"**{entry.Emoji} {entry.DisplayName}**: *[silence]*");
            }
            else
            {
                sb.AppendLine($"#### {entry.Emoji} {entry.DisplayName}");
                sb.AppendLine();
                sb.AppendLine(entry.Content);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
