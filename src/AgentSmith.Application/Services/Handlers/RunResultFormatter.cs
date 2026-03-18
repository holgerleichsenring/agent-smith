using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Formats run artifacts (plan.md and result.md) as markdown strings.
/// Pure formatting — no I/O, no state.
/// </summary>
public static class RunResultFormatter
{
    public static string FormatPlan(Ticket ticket, Plan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Plan: {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Ticket:** #{ticket.Id} — {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(plan.Summary);
        sb.AppendLine();
        sb.AppendLine("## Steps");

        foreach (var step in plan.Steps)
            sb.AppendLine($"{step.Order}. [{step.ChangeType}] {step.Description} → {step.TargetFile}");

        return sb.ToString();
    }

    public static string FormatResult(
        Ticket ticket, Plan plan, IReadOnlyList<CodeChange> changes,
        int runNumber, int durationSeconds, RunCostSummary? costSummary,
        List<ExecutionTrailEntry>? trail, IReadOnlyList<PlanDecision>? decisions = null)
    {
        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";

        var sb = new StringBuilder();
        sb.AppendLine($"# r{runNumber:D2}: {ticket.Title}");
        sb.AppendLine();

        AppendFrontmatter(sb, ticket, changeType, durationSeconds, costSummary);

        sb.AppendLine("## Changed Files");
        foreach (var change in changes)
            sb.AppendLine($"- [{change.ChangeType}] {change.Path}");

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine(plan.Summary);

        AppendDecisions(sb, decisions);
        AppendExecutionTrail(sb, trail);

        return sb.ToString();
    }

    internal static void AppendFrontmatter(
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

    internal static void AppendDecisions(StringBuilder sb, IReadOnlyList<PlanDecision>? decisions)
    {
        if (decisions is null || decisions.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## Decisions");

        var grouped = decisions
            .GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            sb.AppendLine();
            sb.AppendLine($"### {group.Key}");
            foreach (var d in group)
                sb.AppendLine($"- {d.Decision}");
        }
    }

    private static void AppendExecutionTrail(StringBuilder sb, List<ExecutionTrailEntry>? trail)
    {
        if (trail is null || trail.Count == 0) return;

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
}
