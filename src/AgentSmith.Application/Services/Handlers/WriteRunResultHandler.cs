using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
        WriteRunResultContext context, CancellationToken cancellationToken)
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

        context.Pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var costSummary);
        context.Pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out var durationSeconds);

        var sb = new StringBuilder();
        sb.AppendLine($"# r{runNumber:D2}: {context.Ticket.Title}");
        sb.AppendLine();
        BuildFrontmatter(sb, context.Ticket, changeType, durationSeconds, costSummary);
        sb.AppendLine("## Changed Files");

        foreach (var change in context.Changes)
            sb.AppendLine($"- [{change.ChangeType}] {change.Path}");

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(context.Plan.Summary);

        AppendExecutionTrail(sb, context.Pipeline);

        await File.WriteAllTextAsync(
            Path.Combine(runDir, "result.md"), sb.ToString(), ct);
    }

    internal static void BuildFrontmatter(
        StringBuilder sb, Ticket ticket, string changeType,
        int durationSeconds, RunCostSummary? costSummary)
    {
        var ci = CultureInfo.InvariantCulture;

        sb.AppendLine("---");
        sb.AppendLine($"ticket: \"#{ticket.Id} — {ticket.Title}\"");
        sb.AppendLine($"date: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("result: success");
        sb.AppendLine($"type: {changeType}");

        if (durationSeconds > 0)
            sb.AppendLine($"duration_seconds: {durationSeconds}");

        if (costSummary is not null)
        {
            AppendTokenSection(sb, costSummary);
            AppendCostSection(sb, costSummary, ci);
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendTokenSection(StringBuilder sb, RunCostSummary costSummary)
    {
        var totalInput = costSummary.Phases.Values.Sum(p => p.InputTokens);
        var totalOutput = costSummary.Phases.Values.Sum(p => p.OutputTokens);
        var totalCache = costSummary.Phases.Values.Sum(p => p.CacheReadTokens);

        sb.AppendLine("tokens:");
        sb.AppendLine($"  input: {totalInput}");
        sb.AppendLine($"  output: {totalOutput}");
        sb.AppendLine($"  cache_read: {totalCache}");
        sb.AppendLine($"  total: {totalInput + totalOutput + totalCache}");
    }

    private static void AppendCostSection(
        StringBuilder sb, RunCostSummary costSummary, CultureInfo ci)
    {
        sb.AppendLine("cost:");
        sb.AppendLine(string.Format(ci, "  total_usd: {0:F4}", costSummary.TotalCost));
        sb.AppendLine("  phases:");

        foreach (var (phase, cost) in costSummary.Phases)
        {
            sb.AppendLine($"    {phase}:");
            sb.AppendLine($"      model: {cost.Model}");
            sb.AppendLine($"      input: {cost.InputTokens}");
            sb.AppendLine($"      output: {cost.OutputTokens}");
            sb.AppendLine($"      cache_read: {cost.CacheReadTokens}");
            sb.AppendLine($"      turns: {cost.Iterations}");
            sb.AppendLine(string.Format(ci, "      usd: {0:F4}", cost.Cost));
        }
    }

    private static void AppendExecutionTrail(StringBuilder sb, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<ExecutionTrailEntry>>(
                ContextKeys.ExecutionTrail, out var trail) || trail is null || trail.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("## Execution Trail");
        sb.AppendLine();
        sb.AppendLine("| # | Command | Skill | Result | Duration | Inserted |");
        sb.AppendLine("|---|---------|-------|--------|----------|----------|");

        var totalDuration = TimeSpan.Zero;
        for (var i = 0; i < trail.Count; i++)
        {
            var e = trail[i];
            var status = e.Success ? "OK" : "FAIL";
            var msg = e.Message.Length > 40 ? e.Message[..40] + "..." : e.Message;
            var skill = e.Skill ?? "-";
            var duration = $"{e.Duration.TotalSeconds:F1}s";
            var inserted = e.InsertedCommandCount.HasValue ? $"+{e.InsertedCommandCount}" : "-";
            totalDuration += e.Duration;

            sb.AppendLine($"| {i + 1} | {e.CommandName} | {skill} | {status}: {msg} | {duration} | {inserted} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total: {trail.Count} commands, {totalDuration.TotalSeconds:F1}s**");
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
