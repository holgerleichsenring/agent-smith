using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Appends YAML frontmatter, token, and cost sections to a run-result
/// markdown document.
/// </summary>
internal static class RunCostSectionWriter
{
    internal static void AppendFrontmatter(
        StringBuilder sb, Ticket ticket, string changeType,
        int durationSeconds, RunCostSummary? costSummary,
        RunMetaTopology? topology = null)
    {
        var ci = CultureInfo.InvariantCulture;

        sb.AppendLine("---");
        sb.AppendLine($"ticket: \"#{ticket.Id} — {ticket.Title}\"");
        sb.AppendLine($"date: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("result: success");
        sb.AppendLine($"type: {changeType}");

        if (durationSeconds > 0)
            sb.AppendLine($"duration_seconds: {durationSeconds}");

        AppendTopology(sb, topology);

        if (costSummary is not null)
        {
            AppendTokenSection(sb, costSummary);
            AppendCostSection(sb, costSummary, ci);
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendTopology(StringBuilder sb, RunMetaTopology? topology)
    {
        if (topology is null) return;
        if (!string.IsNullOrEmpty(topology.RunId)) sb.AppendLine($"run_id: {topology.RunId}");
        if (!string.IsNullOrEmpty(topology.PipelineName)) sb.AppendLine($"pipeline_name: {topology.PipelineName}");
        if (!string.IsNullOrEmpty(topology.Status)) sb.AppendLine($"status: {topology.Status}");
        if (topology.StartedAt is { } started)
            sb.AppendLine($"started_at: {started.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
        if (!string.IsNullOrEmpty(topology.RepoMode)) sb.AppendLine($"repo_mode: {topology.RepoMode}");
        if (topology.SandboxCount > 0) sb.AppendLine($"sandbox_count: {topology.SandboxCount}");
        if (topology.Repos is { Count: > 0 } repos)
        {
            sb.AppendLine("repos:");
            foreach (var name in repos) sb.AppendLine($"  - {name}");
        }
    }

    /// <summary>
    /// Init-mode variant: no ticket/changeType, but the same tokens + cost
    /// sections so operators get consistent reporting across init / fix / feat.
    /// </summary>
    internal static void AppendInitFrontmatter(
        StringBuilder sb, int durationSeconds, RunCostSummary? costSummary)
    {
        var ci = CultureInfo.InvariantCulture;

        sb.AppendLine("---");
        sb.AppendLine("type: init");
        sb.AppendLine($"date: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("result: success");

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
}
