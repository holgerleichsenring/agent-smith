using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes run artifacts (plan.md + result.md) to .agentsmith/runs/r{NN}-{slug}/
/// and appends the run entry to state.done in .agentsmith/context.yaml.
/// </summary>
public sealed class WriteRunResultHandler(
    ILogger<WriteRunResultHandler> logger)
    : ICommandHandler<WriteRunResultContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string RunsDir = "runs";
    private const string ContextFileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        WriteRunResultContext context, CancellationToken cancellationToken = default)
    {
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runsDir = Path.Combine(agentDir, RunsDir);
        Directory.CreateDirectory(runsDir);

        var contextPath = Path.Combine(agentDir, ContextFileName);
        var nextRunNumber = DetermineNextRunNumber(contextPath);
        var slug = GenerateSlug(context.Ticket.Title);
        var runDirName = $"r{nextRunNumber:D2}-{slug}";
        var runDir = Path.Combine(runsDir, runDirName);
        Directory.CreateDirectory(runDir);

        await WritePlanAsync(runDir, context, cancellationToken);
        await WriteResultAsync(runDir, context, nextRunNumber, cancellationToken);
        await AppendToContextYamlAsync(contextPath, nextRunNumber, context.Ticket, cancellationToken);

        context.Pipeline.Set(ContextKeys.RunNumber, nextRunNumber);

        logger.LogInformation(
            "Written run result r{RunNumber:D2} to {Dir}",
            nextRunNumber, runDirName);

        return CommandResult.Ok($"Run r{nextRunNumber:D2} recorded in {runDirName}");
    }

    internal static int DetermineNextRunNumber(string contextPath)
    {
        if (!File.Exists(contextPath))
            return 1;

        try
        {
            var content = File.ReadAllText(contextPath);
            var matches = Regex.Matches(content, @"^\s+r(\d+):", RegexOptions.Multiline);

            var maxNumber = 0;
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var num) && num > maxNumber)
                    maxNumber = num;
            }

            return maxNumber + 1;
        }
        catch
        {
            return 1;
        }
    }

    internal static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s]+", "-");
        slug = slug.Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }

    private static async Task WritePlanAsync(
        string runDir, WriteRunResultContext context, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Plan: {context.Ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Ticket:** #{context.Ticket.Id} — {context.Ticket.Title}");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(context.Plan.Summary);
        sb.AppendLine();
        sb.AppendLine("## Steps");

        foreach (var step in context.Plan.Steps)
            sb.AppendLine($"{step.Order}. [{step.ChangeType}] {step.Description} → {step.TargetFile}");

        await File.WriteAllTextAsync(
            Path.Combine(runDir, "plan.md"), sb.ToString(), ct);
    }

    private static async Task WriteResultAsync(
        string runDir, WriteRunResultContext context, int runNumber, CancellationToken ct)
    {
        var changeType = context.Ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";

        var sb = new StringBuilder();
        sb.AppendLine($"# r{runNumber:D2}: {context.Ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"- **Ticket**: #{context.Ticket.Id} — {context.Ticket.Title}");
        sb.AppendLine($"- **Date**: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"- **Result**: success");
        sb.AppendLine($"- **Type**: {changeType}");
        sb.AppendLine();
        sb.AppendLine("## Changed Files");

        foreach (var change in context.Changes)
            sb.AppendLine($"- [{change.ChangeType}] {change.Path}");

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(context.Plan.Summary);

        await File.WriteAllTextAsync(
            Path.Combine(runDir, "result.md"), sb.ToString(), ct);
    }

    private static async Task AppendToContextYamlAsync(
        string contextPath, int runNumber, Ticket ticket, CancellationToken ct)
    {
        if (!File.Exists(contextPath))
            return;

        var content = await File.ReadAllTextAsync(contextPath, ct);

        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";
        var entry = $"    r{runNumber:D2}: \"{changeType} #{ticket.Id}: {ticket.Title}\"";

        // Insert before "active:" or "planned:" line in state section
        var insertPattern = new Regex(@"^(\s+active:)", RegexOptions.Multiline);
        var match = insertPattern.Match(content);

        if (match.Success)
        {
            content = content.Insert(match.Index, entry + "\n");
        }

        await File.WriteAllTextAsync(contextPath, content, ct);
    }
}
